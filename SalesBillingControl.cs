using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace MeroDokan
{
    public class SalesBillingControl : UserControl
    {
        // Event for requesting navigation to Appointments
        public event Action OnOpenAppointmentsRequested;

        // Top Filter Bar & Barcode Scanner
        private Button btnModeProducts;
        private Button btnOpenAppointments;
        private TextBox txtBarcodeScan;
        private Button btnAddCustomItem;

        // Categories Panel
        private FlowLayoutPanel flowCategories;
        private Label lblCatTitle;
        private string selectedCategory = "All";

        // Products Grid Panel
        private FlowLayoutPanel flowItemsGrid;
        private Label lblItemsGridTitle;

        // Right Side Cart Panel
        private Panel rightCartPanel;
        private Label lblCustomerName;
        private Label lblCustomerPhone;
        private Label lblOrderItemsCount;
        private Button btnClearCart;
        private FlowLayoutPanel flowCartItems;

        // Bill Mode Switch (GST vs Non-GST)
        private Button btnBillModeGST;
        private Button btnBillModeNonGST;
        private bool isGSTBillMode = true;

        // Calculation & Checkout
        private Label lblSubTotalVal;
        private TextBox txtDiscountVal;
        private ComboBox comboDiscountType;
        private Label lblDiscountCalculated;
        private Label lblTaxableTitle;
        private Label lblTaxableVal;
        private Label lblTaxBreakdownTitle;
        private Label lblTaxCalculated;
        private Label lblTotalPayableVal;

        // Payment Mode Buttons
        private Button btnPayCash;
        private Button btnPayUPI;
        private Button btnPayCard;
        private Button btnPaySplit;
        private string selectedPaymentMethod = "Cash";
        private decimal splitCashAmount = 0;
        private decimal splitOnlineAmount = 0;

        private Button btnPayAndPrint;

        // Active customer & appointment checkout state
        private int currentCustomerId = 1;
        private string currentCustomerName = "Walk-in Customer";
        private string currentCustomerPhone = "+977-9800000000";
        private string currentCustomerGSTIN = "";
        private string currentCustomerStateName = "Delhi";
        private string currentCustomerStateCode = "07";
        private int currentAppointmentId = 0;
        private int editingSaleId = 0;
        private string editingInvoiceNumber = "";
        private Panel editBannerPanel;
        private Label lblEditBanner;
        private Button btnCancelEdit;

        // Salon Profile & GST Config
        private string salonShopName = "Glamour Salon & Spa";
        private string salonAddress = "Kathmandu, Nepal";
        private string salonPhone = "+977-1-4200000";
        private string salonEmail = "contact@merosaloon.com";
        private string salonGSTIN = "";
        private string salonStateName = "Delhi";
        private string salonStateCode = "07";
        private bool isTaxInclusive = true;
        private string defaultBillType = "GST";
        private decimal defaultGSTRate = 18.00m;
        private string salonUPIId = "";
        private string salonUPIName = "";
        private bool salonAutoShowQR = true;
        private bool salonPrintQROnReceipt = true;

        public class CartItem
        {
            public string ItemType { get; set; } // "Service" or "Product"
            public int ItemId { get; set; }
            public string Code { get; set; }
            public string HSNSAC { get; set; }
            public string Name { get; set; }
            public int StaffId { get; set; }
            public string StaffName { get; set; }
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
            public decimal Total => Quantity * UnitPrice;
            public decimal GSTRate { get; set; } = 18.00m;
            public decimal TaxableAmount { get; set; } = 0.00m;
            public decimal CGSTAmount { get; set; } = 0.00m;
            public decimal SGSTAmount { get; set; } = 0.00m;
            public decimal IGSTAmount { get; set; } = 0.00m;
            public decimal CostPrice { get; set; }
            public string IconEmoji { get; set; }
        }

        private List<CartItem> cartItems = new List<CartItem>();
        private List<StaffMember> staffList = new List<StaffMember>();

        public class ComboBoxItem
        {
            public int Id { get; set; }
            public string Display { get; set; }
            public decimal Price { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public override string ToString() => Display;
        }

        public class StaffMember
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Role { get; set; }
        }

        public class ServiceItemData
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string SACCode { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public decimal GSTRate { get; set; }
            public decimal Price { get; set; }
            public int DurationMinutes { get; set; }
            public string IconEmoji { get; set; }
        }

        public class ProductItemData
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string HSNCode { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public decimal GSTRate { get; set; }
            public decimal Price { get; set; }
            public decimal CostPrice { get; set; }
            public int Stock { get; set; }
            public string IconEmoji { get; set; }
        }

        private List<ServiceItemData> allServices = new List<ServiceItemData>();
        private List<ProductItemData> allProducts = new List<ProductItemData>();

        // Print Document
        private PrintDocument invoiceDoc;
        private PrintPreviewDialog previewDlg;
        private int lastSaleId = 0;

        public SalesBillingControl()
        {
            InitializeComponent();
            LoadSalonGSTProfile();
            LoadStaffList();
            LoadServicesAndProducts();
            LoadCategories("Products");
            RenderItemsGrid("Products");
            UpdateCartUI();

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    EnsureWalkInCustomer(conn);
                }
            }
            catch { }

            this.Load += (s, e) => {
                txtBarcodeScan?.Focus();
            };
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Theme.Secondary;
            this.DoubleBuffered = true;

            // ==========================================
            // 1. RIGHT CART & CHECKOUT PANEL (Dock Right)
            // ==========================================
            rightCartPanel = new Panel();
            rightCartPanel.Width = 380;
            rightCartPanel.Dock = DockStyle.Right;
            rightCartPanel.BackColor = Theme.CardBg;
            rightCartPanel.Padding = new Padding(12);
            rightCartPanel.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.CardBorder, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, 0, rightCartPanel.Height);
                }
            };

            // 1a-0. Adjusting Saved Invoice Banner (Dock Top, Hidden by default)
            editBannerPanel = new Panel();
            editBannerPanel.Height = 36;
            editBannerPanel.Dock = DockStyle.Top;
            editBannerPanel.BackColor = Color.FromArgb(180, 83, 9); // Amber
            editBannerPanel.Margin = new Padding(0, 0, 0, 6);
            editBannerPanel.Visible = false;

            lblEditBanner = new Label();
            lblEditBanner.Text = "✏️ Adjusting Saved Invoice";
            lblEditBanner.Location = new Point(8, 9);
            lblEditBanner.AutoSize = true;
            lblEditBanner.BackColor = Color.Transparent;
            Theme.StyleLabel(lblEditBanner, Color.White, new Font("Segoe UI", 8.5F, FontStyle.Bold));
            editBannerPanel.Controls.Add(lblEditBanner);

            btnCancelEdit = new Button();
            btnCancelEdit.Text = "✖ Cancel Edit";
            btnCancelEdit.Size = new Size(95, 24);
            btnCancelEdit.Location = new Point(255, 6);
            btnCancelEdit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancelEdit.FlatStyle = FlatStyle.Flat;
            btnCancelEdit.BackColor = Color.FromArgb(30, 41, 59);
            btnCancelEdit.ForeColor = Color.White;
            btnCancelEdit.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            btnCancelEdit.FlatAppearance.BorderSize = 0;
            btnCancelEdit.Cursor = Cursors.Hand;
            btnCancelEdit.Click += (s, e) => ExitEditMode();
            editBannerPanel.Controls.Add(btnCancelEdit);

            rightCartPanel.Controls.Add(editBannerPanel);

            // 1a. Customer Tile (Dock Top)
            Panel customerCard = new Panel();
            customerCard.Height = 54;
            customerCard.Dock = DockStyle.Top;
            customerCard.BackColor = Theme.InputBg;
            customerCard.Margin = new Padding(0, 0, 0, 8);
            customerCard.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.CardBorder, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, customerCard.Width - 1, customerCard.Height - 1);
                }
            };

            Label lblCustAvatar = new Label();
            lblCustAvatar.Text = "👤";
            lblCustAvatar.Font = new Font("Segoe UI", 16F);
            lblCustAvatar.Location = new Point(10, 10);
            lblCustAvatar.Size = new Size(32, 32);
            lblCustAvatar.BackColor = Color.Transparent;
            customerCard.Controls.Add(lblCustAvatar);

            lblCustomerName = new Label();
            lblCustomerName.Text = "Walk-in Customer";
            lblCustomerName.Location = new Point(46, 8);
            lblCustomerName.AutoSize = true;
            lblCustomerName.BackColor = Color.Transparent;
            Theme.StyleLabel(lblCustomerName, Theme.TextWhite, new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold));
            customerCard.Controls.Add(lblCustomerName);

            lblCustomerPhone = new Label();
            lblCustomerPhone.Text = "+977-9800000000";
            lblCustomerPhone.Location = new Point(48, 28);
            lblCustomerPhone.AutoSize = true;
            lblCustomerPhone.BackColor = Color.Transparent;
            Theme.StyleLabel(lblCustomerPhone, Theme.TextMuted, new Font("Segoe UI", 8F));
            customerCard.Controls.Add(lblCustomerPhone);

            Button btnSelectCust = new Button();
            btnSelectCust.Text = "👥";
            btnSelectCust.Size = new Size(32, 32);
            btnSelectCust.Location = new Point(315, 11);
            btnSelectCust.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Theme.StyleButton(btnSelectCust, Theme.CardBg, Theme.TextWhite);
            btnSelectCust.Click += BtnSelectCust_Click;
            customerCard.Controls.Add(btnSelectCust);

            Button btnAddCust = new Button();
            btnAddCust.Text = "+";
            btnAddCust.Size = new Size(32, 32);
            btnAddCust.Location = new Point(275, 11);
            btnAddCust.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Theme.StyleButton(btnAddCust, Theme.Success, Theme.TextWhite);
            btnAddCust.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnAddCust.Click += BtnAddCust_Click;
            customerCard.Controls.Add(btnAddCust);

            rightCartPanel.Controls.Add(customerCard);

            // 1b. Order Items Header Banner (Dock Top)
            Panel orderBanner = new Panel();
            orderBanner.Height = 36;
            orderBanner.Dock = DockStyle.Top;
            orderBanner.BackColor = Theme.Accent; // Electric Orange banner
            orderBanner.Margin = new Padding(0, 8, 0, 6);

            lblOrderItemsCount = new Label();
            lblOrderItemsCount.Text = "Order Items ( 0 )";
            lblOrderItemsCount.Location = new Point(10, 9);
            lblOrderItemsCount.AutoSize = true;
            lblOrderItemsCount.BackColor = Color.Transparent;
            Theme.StyleLabel(lblOrderItemsCount, Theme.TextWhite, new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold));
            orderBanner.Controls.Add(lblOrderItemsCount);

            Button btnAddExtra = new Button();
            btnAddExtra.Text = "➕ Extra";
            btnAddExtra.Size = new Size(72, 26);
            btnAddExtra.Location = new Point(188, 5);
            btnAddExtra.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddExtra.FlatStyle = FlatStyle.Flat;
            btnAddExtra.BackColor = Color.FromArgb(30, 41, 59);
            btnAddExtra.ForeColor = Theme.Accent;
            btnAddExtra.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            btnAddExtra.FlatAppearance.BorderSize = 0;
            btnAddExtra.Cursor = Cursors.Hand;
            btnAddExtra.Click += (s, e) => ShowAddExtraChargeDialog();
            orderBanner.Controls.Add(btnAddExtra);

            btnClearCart = new Button();
            btnClearCart.Text = "🗑️ Clear";
            btnClearCart.Size = new Size(68, 26);
            btnClearCart.Location = new Point(266, 5);
            btnClearCart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearCart.FlatStyle = FlatStyle.Flat;
            btnClearCart.BackColor = Color.Transparent;
            btnClearCart.ForeColor = Theme.TextWhite;
            btnClearCart.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            btnClearCart.FlatAppearance.BorderSize = 0;
            btnClearCart.Click += (s, e) => { cartItems.Clear(); splitCashAmount = 0; splitOnlineAmount = 0; UpdateCartUI(); };
            orderBanner.Controls.Add(btnClearCart);

            rightCartPanel.Controls.Add(orderBanner);

            // 1c. Bottom Checkout & Totals Area (Dock Bottom)
            Panel bottomCheckoutArea = new Panel();
            bottomCheckoutArea.Height = 325;
            bottomCheckoutArea.Dock = DockStyle.Bottom;
            bottomCheckoutArea.BackColor = Color.Transparent;

            // Bill Mode Switch Bar (GST Invoice vs Non-GST Bill)
            Panel billModeBar = new Panel();
            billModeBar.Size = new Size(356, 32);
            billModeBar.Location = new Point(0, 0);
            billModeBar.BackColor = Color.Transparent;

            btnBillModeGST = new Button();
            btnBillModeGST.Text = "🧾 GST Invoice";
            btnBillModeGST.Size = new Size(174, 30);
            btnBillModeGST.Location = new Point(0, 0);
            btnBillModeGST.FlatStyle = FlatStyle.Flat;
            btnBillModeGST.BackColor = Theme.Accent;
            btnBillModeGST.ForeColor = Color.White;
            btnBillModeGST.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
            btnBillModeGST.FlatAppearance.BorderSize = 0;
            btnBillModeGST.Cursor = Cursors.Hand;
            btnBillModeGST.Click += (s, e) => SetBillMode(true);
            billModeBar.Controls.Add(btnBillModeGST);

            btnBillModeNonGST = new Button();
            btnBillModeNonGST.Text = "📄 Non-GST Bill";
            btnBillModeNonGST.Size = new Size(174, 30);
            btnBillModeNonGST.Location = new Point(178, 0);
            btnBillModeNonGST.FlatStyle = FlatStyle.Flat;
            btnBillModeNonGST.BackColor = Theme.CardBg;
            btnBillModeNonGST.ForeColor = Theme.TextMuted;
            btnBillModeNonGST.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
            btnBillModeNonGST.FlatAppearance.BorderSize = 0;
            btnBillModeNonGST.Cursor = Cursors.Hand;
            btnBillModeNonGST.Click += (s, e) => SetBillMode(false);
            billModeBar.Controls.Add(btnBillModeNonGST);

            bottomCheckoutArea.Controls.Add(billModeBar);

            // Calculation Card
            Panel calcCard = new Panel();
            calcCard.Size = new Size(356, 172);
            calcCard.Location = new Point(0, 35);
            calcCard.BackColor = Theme.InputBg;
            calcCard.Padding = new Padding(10);
            calcCard.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.CardBorder, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, calcCard.Width - 1, calcCard.Height - 1);
                }
            };

            Label lblSubT = new Label();
            lblSubT.Text = "Sub Total";
            lblSubT.Location = new Point(10, 8);
            lblSubT.AutoSize = true;
            lblSubT.BackColor = Color.Transparent;
            Theme.StyleLabel(lblSubT, Theme.TextMuted, Theme.MainFont);
            calcCard.Controls.Add(lblSubT);

            lblSubTotalVal = new Label();
            lblSubTotalVal.Text = "Rs. 0.00";
            lblSubTotalVal.AutoSize = false;
            lblSubTotalVal.Size = new Size(145, 20);
            lblSubTotalVal.Location = new Point(200, 8);
            lblSubTotalVal.TextAlign = ContentAlignment.MiddleRight;
            lblSubTotalVal.BackColor = Color.Transparent;
            Theme.StyleLabel(lblSubTotalVal, Theme.TextWhite, Theme.BoldFont);
            calcCard.Controls.Add(lblSubTotalVal);

            Label lblDisc = new Label();
            lblDisc.Text = "Discount";
            lblDisc.Location = new Point(10, 32);
            lblDisc.AutoSize = true;
            lblDisc.BackColor = Color.Transparent;
            Theme.StyleLabel(lblDisc, Theme.TextMuted, Theme.MainFont);
            calcCard.Controls.Add(lblDisc);

            comboDiscountType = new ComboBox();
            comboDiscountType.Size = new Size(45, 22);
            comboDiscountType.Location = new Point(75, 29);
            comboDiscountType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboDiscountType.Items.AddRange(new object[] { "%", "Rs" });
            comboDiscountType.SelectedIndex = 0;
            Theme.StyleComboBox(comboDiscountType);
            comboDiscountType.SelectedIndexChanged += (s, e) => RecalculateTotals();
            calcCard.Controls.Add(comboDiscountType);

            txtDiscountVal = new TextBox();
            txtDiscountVal.Size = new Size(55, 22);
            txtDiscountVal.Location = new Point(125, 29);
            txtDiscountVal.Text = "0";
            Theme.StyleTextBox(txtDiscountVal);
            txtDiscountVal.TextChanged += (s, e) => RecalculateTotals();
            calcCard.Controls.Add(txtDiscountVal);

            lblDiscountCalculated = new Label();
            lblDiscountCalculated.Text = "- Rs. 0.00";
            lblDiscountCalculated.AutoSize = false;
            lblDiscountCalculated.Size = new Size(145, 20);
            lblDiscountCalculated.Location = new Point(200, 32);
            lblDiscountCalculated.TextAlign = ContentAlignment.MiddleRight;
            lblDiscountCalculated.BackColor = Color.Transparent;
            Theme.StyleLabel(lblDiscountCalculated, Theme.Success, Theme.BoldFont);
            calcCard.Controls.Add(lblDiscountCalculated);

            // Taxable Net Value
            lblTaxableTitle = new Label();
            lblTaxableTitle.Text = "Taxable Value";
            lblTaxableTitle.Location = new Point(10, 56);
            lblTaxableTitle.AutoSize = true;
            lblTaxableTitle.BackColor = Color.Transparent;
            Theme.StyleLabel(lblTaxableTitle, Theme.TextMuted, Theme.MainFont);
            calcCard.Controls.Add(lblTaxableTitle);

            lblTaxableVal = new Label();
            lblTaxableVal.Text = "Rs. 0.00";
            lblTaxableVal.AutoSize = false;
            lblTaxableVal.Size = new Size(145, 20);
            lblTaxableVal.Location = new Point(200, 56);
            lblTaxableVal.TextAlign = ContentAlignment.MiddleRight;
            lblTaxableVal.BackColor = Color.Transparent;
            Theme.StyleLabel(lblTaxableVal, Theme.TextLight, Theme.MainFont);
            calcCard.Controls.Add(lblTaxableVal);

            // GST Breakdown
            lblTaxBreakdownTitle = new Label();
            lblTaxBreakdownTitle.Text = "GST Tax";
            lblTaxBreakdownTitle.Location = new Point(10, 80);
            lblTaxBreakdownTitle.AutoSize = true;
            lblTaxBreakdownTitle.BackColor = Color.Transparent;
            Theme.StyleLabel(lblTaxBreakdownTitle, Theme.TextMuted, Theme.MainFont);
            calcCard.Controls.Add(lblTaxBreakdownTitle);

            lblTaxCalculated = new Label();
            lblTaxCalculated.Text = "Rs. 0.00";
            lblTaxCalculated.AutoSize = false;
            lblTaxCalculated.Size = new Size(145, 20);
            lblTaxCalculated.Location = new Point(200, 80);
            lblTaxCalculated.TextAlign = ContentAlignment.MiddleRight;
            lblTaxCalculated.BackColor = Color.Transparent;
            Theme.StyleLabel(lblTaxCalculated, Theme.Accent, Theme.BoldFont);
            calcCard.Controls.Add(lblTaxCalculated);

            Panel lineDiv = new Panel();
            lineDiv.Size = new Size(336, 1);
            lineDiv.Location = new Point(10, 106);
            lineDiv.BackColor = Theme.CardBorder;
            calcCard.Controls.Add(lineDiv);

            Label lblPayable = new Label();
            lblPayable.Text = "Total Payable";
            lblPayable.Location = new Point(10, 120);
            lblPayable.AutoSize = true;
            lblPayable.BackColor = Color.Transparent;
            Theme.StyleLabel(lblPayable, Theme.TextWhite, new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold));
            calcCard.Controls.Add(lblPayable);

            lblTotalPayableVal = new Label();
            lblTotalPayableVal.Text = "Rs. 0.00";
            lblTotalPayableVal.AutoSize = false;
            lblTotalPayableVal.Size = new Size(185, 30);
            lblTotalPayableVal.Location = new Point(160, 115);
            lblTotalPayableVal.TextAlign = ContentAlignment.MiddleRight;
            lblTotalPayableVal.BackColor = Color.Transparent;
            Theme.StyleLabel(lblTotalPayableVal, Theme.Accent, new Font("Segoe UI", 15F, FontStyle.Bold));
            calcCard.Controls.Add(lblTotalPayableVal);

            bottomCheckoutArea.Controls.Add(calcCard);

            // Payment Tiles (Cash, UPI, Card, Split)
            Panel payTilesPanel = new Panel();
            payTilesPanel.Size = new Size(356, 44);
            payTilesPanel.Location = new Point(0, 214);
            payTilesPanel.BackColor = Color.Transparent;

            btnPayCash = CreatePaymentTile("💵\nCash", 0, Theme.Success);
            btnPayCash.Size = new Size(84, 44);
            btnPayCash.Click += (s, e) => SelectPaymentMode("Cash");
            payTilesPanel.Controls.Add(btnPayCash);

            btnPayUPI = CreatePaymentTile("📱\nUPI", 90, Theme.UPIColor);
            btnPayUPI.Size = new Size(84, 44);
            btnPayUPI.Click += (s, e) => SelectPaymentMode("QR Pay / UPI");
            payTilesPanel.Controls.Add(btnPayUPI);

            btnPayCard = CreatePaymentTile("💳\nCard", 180, Theme.Info);
            btnPayCard.Size = new Size(84, 44);
            btnPayCard.Click += (s, e) => SelectPaymentMode("Card");
            payTilesPanel.Controls.Add(btnPayCard);

            btnPaySplit = CreatePaymentTile("🔀\nSplit", 270, Color.FromArgb(249, 115, 22));
            btnPaySplit.Size = new Size(86, 44);
            btnPaySplit.Click += (s, e) => SelectPaymentMode("Split");
            payTilesPanel.Controls.Add(btnPaySplit);

            bottomCheckoutArea.Controls.Add(payTilesPanel);

            // Pay & Print Button
            btnPayAndPrint = new Button();
            btnPayAndPrint.Text = "🖨️  PAY & PRINT ( Rs. 0.00 )";
            btnPayAndPrint.Size = new Size(356, 46);
            btnPayAndPrint.Location = new Point(0, 264);
            Theme.StylePrimaryButton(btnPayAndPrint);
            btnPayAndPrint.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnPayAndPrint.Click += BtnPayAndPrint_Click;
            bottomCheckoutArea.Controls.Add(btnPayAndPrint);

            rightCartPanel.Controls.Add(bottomCheckoutArea);

            // 1d. Cart Items List (Dock Fill in middle)
            flowCartItems = new FlowLayoutPanel();
            flowCartItems.Dock = DockStyle.Fill;
            flowCartItems.BackColor = Color.Transparent;
            flowCartItems.AutoScroll = true;
            flowCartItems.FlowDirection = FlowDirection.TopDown;
            flowCartItems.WrapContents = false;
            flowCartItems.Padding = new Padding(0, 8, 0, 8);
            rightCartPanel.Controls.Add(flowCartItems);

            // Z-Order for Right Panel
            customerCard.SendToBack();
            orderBanner.SendToBack();
            bottomCheckoutArea.SendToBack();
            flowCartItems.BringToFront();

            // ==========================================
            // 2. LEFT MAIN CATALOG CONTAINER (Dock Fill)
            // ==========================================
            Panel leftCatalogPanel = new Panel();
            leftCatalogPanel.Dock = DockStyle.Fill;
            leftCatalogPanel.BackColor = Color.Transparent;
            leftCatalogPanel.Padding = new Padding(15, 10, 15, 10);

            // 2a. Top Mode Tabs Header (Dock Top)
            Panel topTabsBar = new Panel();
            topTabsBar.Height = 44;
            topTabsBar.Dock = DockStyle.Top;
            topTabsBar.BackColor = Color.Transparent;

            btnModeProducts = new Button();
            btnModeProducts.Text = "🛍️  RETAIL PRODUCTS";
            btnModeProducts.Size = new Size(160, 36);
            btnModeProducts.Location = new Point(0, 0);
            Theme.StyleButton(btnModeProducts, Theme.Accent, Theme.TextWhite);
            btnModeProducts.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnModeProducts.Cursor = Cursors.Default;
            topTabsBar.Controls.Add(btnModeProducts);

            btnOpenAppointments = new Button();
            btnOpenAppointments.Text = "📅  Go To Appointments (Services)";
            btnOpenAppointments.Size = new Size(245, 36);
            btnOpenAppointments.Location = new Point(168, 0);
            Theme.StyleButton(btnOpenAppointments, Color.FromArgb(30, 41, 59), Theme.Accent);
            btnOpenAppointments.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            btnOpenAppointments.Cursor = Cursors.Hand;
            btnOpenAppointments.Click += (s, e) => OnOpenAppointmentsRequested?.Invoke();
            topTabsBar.Controls.Add(btnOpenAppointments);

            // Barcode Scanner Box (Prominent & Auto-focused)
            Panel scanContainer = new Panel();
            scanContainer.Location = new Point(422, 0);
            scanContainer.Size = new Size(224, 36);
            scanContainer.BackColor = Theme.InputBg;
            scanContainer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            scanContainer.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.Accent, 1.5f))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, scanContainer.Width - 1, scanContainer.Height - 1);
                }
            };

            Label lblScanIco = new Label();
            lblScanIco.Text = "🏷️";
            lblScanIco.Font = new Font("Segoe UI Emoji", 10F);
            lblScanIco.Location = new Point(6, 7);
            lblScanIco.Size = new Size(22, 22);
            lblScanIco.BackColor = Color.Transparent;
            scanContainer.Controls.Add(lblScanIco);

            txtBarcodeScan = new TextBox();
            txtBarcodeScan.BorderStyle = BorderStyle.None;
            txtBarcodeScan.BackColor = Theme.InputBg;
            txtBarcodeScan.ForeColor = Theme.TextWhite;
            txtBarcodeScan.Font = Theme.BoldFont;
            txtBarcodeScan.Location = new Point(30, 8);
            txtBarcodeScan.Size = new Size(145, 20);
            txtBarcodeScan.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            /* ================= WINDOWS 7 COMPATIBILITY CHANGE (.NET 8 API replaced) =================
            txtBarcodeScan.PlaceholderText = "Scan Barcode / Code (F2)...";
            ================================================================================ */
            Win7Compat.SetPlaceholder(txtBarcodeScan, "Scan Barcode / Code (F2)...");
            txtBarcodeScan.KeyDown += TxtBarcodeScan_KeyDown;
            scanContainer.Controls.Add(txtBarcodeScan);

            Button btnScanEnter = new Button();
            btnScanEnter.Text = "↵ Add";
            btnScanEnter.Size = new Size(54, 28);
            btnScanEnter.Location = new Point(scanContainer.Width - 58, 4);
            btnScanEnter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Theme.StyleButton(btnScanEnter, Theme.Accent, Theme.TextWhite);
            btnScanEnter.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            btnScanEnter.Click += (s, e) => ProcessBarcodeScan(txtBarcodeScan.Text);
            scanContainer.Controls.Add(btnScanEnter);

            topTabsBar.Controls.Add(scanContainer);

            btnAddCustomItem = new Button();
            btnAddCustomItem.Text = "+ Custom Product";
            btnAddCustomItem.Size = new Size(130, 36);
            btnAddCustomItem.Location = new Point(topTabsBar.Width - 135, 0);
            btnAddCustomItem.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Theme.StyleButton(btnAddCustomItem, Theme.Success, Theme.TextWhite);
            btnAddCustomItem.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnAddCustomItem.Click += BtnAddCustomItem_Click;
            topTabsBar.Controls.Add(btnAddCustomItem);

            // 2b. Categories Title (Dock Top)
            lblCatTitle = new Label();
            lblCatTitle.Text = "Product Categories";
            lblCatTitle.Height = 28;
            lblCatTitle.Dock = DockStyle.Top;
            lblCatTitle.BackColor = Color.Transparent;
            Theme.StyleLabel(lblCatTitle, Theme.TextLight, Theme.SubHeaderFont);

            // 2c. Category Cards Flow Panel (Dock Top)
            flowCategories = new FlowLayoutPanel();
            flowCategories.Height = 155;
            flowCategories.Dock = DockStyle.Top;
            flowCategories.BackColor = Color.Transparent;
            flowCategories.AutoScroll = true;

            // 2d. Popular Services / Products Title (Dock Top)
            lblItemsGridTitle = new Label();
            lblItemsGridTitle.Text = "Retail Beauty Products";
            lblItemsGridTitle.Height = 28;
            lblItemsGridTitle.Dock = DockStyle.Top;
            lblItemsGridTitle.BackColor = Color.Transparent;
            Theme.StyleLabel(lblItemsGridTitle, Theme.TextLight, Theme.SubHeaderFont);

            // 2e. Items Grid Flow Panel (Dock Fill)
            flowItemsGrid = new FlowLayoutPanel();
            flowItemsGrid.Dock = DockStyle.Fill;
            flowItemsGrid.BackColor = Color.Transparent;
            flowItemsGrid.AutoScroll = true;

            // Add in reverse docking sequence to guarantee correct visual top-to-bottom layout
            leftCatalogPanel.Controls.Add(flowItemsGrid);
            leftCatalogPanel.Controls.Add(lblItemsGridTitle);
            leftCatalogPanel.Controls.Add(flowCategories);
            leftCatalogPanel.Controls.Add(lblCatTitle);
            leftCatalogPanel.Controls.Add(topTabsBar);

            // Add main panels in precise docking order
            this.Controls.Add(leftCatalogPanel);
            this.Controls.Add(rightCartPanel);

            rightCartPanel.SendToBack();
            leftCatalogPanel.BringToFront();

            // Setup Print Elements
            invoiceDoc = new PrintDocument();
            invoiceDoc.PrintPage += InvoiceDoc_PrintPage;
            previewDlg = new PrintPreviewDialog();
            previewDlg.Document = invoiceDoc;
            previewDlg.Size = new Size(600, 700);
        }

        private Button CreatePaymentTile(string title, int x, Color color)
        {
            Button btn = new Button();
            btn.Text = title;
            btn.Size = new Size(82, 46);
            btn.Location = new Point(x, 0);
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = color;
            btn.ForeColor = Color.White;
            btn.Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold);
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private void SelectPaymentMode(string mode, bool triggerQRPopup = true)
        {
            selectedPaymentMethod = mode;
            btnPayCash.FlatAppearance.BorderSize = (mode == "Cash") ? 2 : 0;
            btnPayUPI.FlatAppearance.BorderSize = (mode == "QR Pay / UPI") ? 2 : 0;
            btnPayCard.FlatAppearance.BorderSize = (mode == "Card") ? 2 : 0;
            btnPaySplit.FlatAppearance.BorderSize = (mode == "Split") ? 2 : 0;

            if (mode == "QR Pay / UPI" && triggerQRPopup && salonAutoShowQR && cartItems.Count > 0)
            {
                ShowDynamicQRPaymentModal();
            }
            else if (mode == "Split" && cartItems.Count > 0)
            {
                ShowSplitPaymentModal();
            }
        }

        public bool ShowSplitPaymentModal(bool forcePrompt = false)
        {
            if (cartItems.Count == 0)
            {
                MessageBox.Show("Order cart is empty! Add services or products before setting split payment.", "Empty Cart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            RecalculateTotals();
            decimal subTotal = 0;
            foreach (var item in cartItems) subTotal += item.Total;

            decimal.TryParse(txtDiscountVal.Text.Trim(), out decimal discVal);
            decimal discountAmt = (comboDiscountType.SelectedItem?.ToString() == "%") ? subTotal * (discVal / 100m) : discVal;
            if (discountAmt > subTotal) discountAmt = subTotal;

            decimal totalTaxable = 0;
            decimal totalCGST = 0;
            decimal totalSGST = 0;
            decimal totalIGST = 0;
            foreach (var item in cartItems)
            {
                totalTaxable += item.TaxableAmount;
                totalCGST += item.CGSTAmount;
                totalSGST += item.SGSTAmount;
                totalIGST += item.IGSTAmount;
            }
            decimal totalTax = totalCGST + totalSGST + totalIGST;
            decimal grandTotal = (!isGSTBillMode || isTaxInclusive) ? Math.Max(0, subTotal - discountAmt) : (totalTaxable + totalTax);

            if (grandTotal <= 0)
            {
                MessageBox.Show("Total payable amount is zero.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            using (Form dlg = new Form())
            {
                dlg.Text = "Split Payment Breakdown (Cash + Online)";
                dlg.Size = new Size(430, 390);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Theme.Secondary;
                dlg.ForeColor = Theme.TextLight;
                dlg.Font = Theme.MainFont;
                dlg.Icon = Theme.AppIcon;

                Label lblHeader = new Label();
                lblHeader.Text = "🔀  Split Invoice Tender";
                lblHeader.Location = new Point(20, 16);
                lblHeader.AutoSize = true;
                Theme.StyleLabel(lblHeader, Theme.TextLight, Theme.SubHeaderFont);
                dlg.Controls.Add(lblHeader);

                Label lblTotalDue = new Label();
                lblTotalDue.Text = $"Total Due: Rs. {grandTotal:N2}";
                lblTotalDue.Location = new Point(20, 46);
                lblTotalDue.AutoSize = true;
                Theme.StyleLabel(lblTotalDue, Theme.Accent, new Font("Segoe UI", 12F, FontStyle.Bold));
                dlg.Controls.Add(lblTotalDue);

                // Cash Input Group
                Label lblCash = new Label();
                lblCash.Text = "💵 Cash Amount (Rs.):";
                lblCash.Location = new Point(20, 80);
                lblCash.AutoSize = true;
                Theme.StyleLabel(lblCash, Theme.TextLight, Theme.BoldFont);
                dlg.Controls.Add(lblCash);

                TextBox txtCash = new TextBox();
                txtCash.Location = new Point(20, 102);
                txtCash.Size = new Size(375, 26);
                Theme.StyleTextBox(txtCash);
                txtCash.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                dlg.Controls.Add(txtCash);

                // Online Input Group
                Label lblOnline = new Label();
                lblOnline.Text = "📱 Online / UPI / Card Amount (Rs.):";
                lblOnline.Location = new Point(20, 138);
                lblOnline.AutoSize = true;
                Theme.StyleLabel(lblOnline, Theme.TextLight, Theme.BoldFont);
                dlg.Controls.Add(lblOnline);

                TextBox txtOnline = new TextBox();
                txtOnline.Location = new Point(20, 160);
                txtOnline.Size = new Size(375, 26);
                Theme.StyleTextBox(txtOnline);
                txtOnline.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                dlg.Controls.Add(txtOnline);

                // Quick Preset Buttons
                FlowLayoutPanel quickBar = new FlowLayoutPanel();
                quickBar.Location = new Point(20, 196);
                quickBar.Size = new Size(375, 34);
                quickBar.BackColor = Color.Transparent;

                Button btnHalf = new Button { Text = "50% / 50%", Size = new Size(100, 28) };
                Theme.StyleSecondaryButton(btnHalf);
                btnHalf.Font = new Font("Segoe UI", 7.8F, FontStyle.Bold);
                quickBar.Controls.Add(btnHalf);

                Button btnFullCash = new Button { Text = "All Cash", Size = new Size(85, 28) };
                Theme.StyleSecondaryButton(btnFullCash);
                btnFullCash.Font = new Font("Segoe UI", 7.8F, FontStyle.Bold);
                quickBar.Controls.Add(btnFullCash);

                Button btnFullOnline = new Button { Text = "All Online", Size = new Size(85, 28) };
                Theme.StyleSecondaryButton(btnFullOnline);
                btnFullOnline.Font = new Font("Segoe UI", 7.8F, FontStyle.Bold);
                quickBar.Controls.Add(btnFullOnline);

                dlg.Controls.Add(quickBar);

                // Live Balance Status Label
                Label lblStatus = new Label();
                lblStatus.Location = new Point(20, 234);
                lblStatus.Size = new Size(375, 24);
                lblStatus.TextAlign = ContentAlignment.MiddleLeft;
                Theme.StyleLabel(lblStatus, Theme.Success, Theme.BoldFont);
                dlg.Controls.Add(lblStatus);

                // Bottom Action Buttons
                Button btnSaveSplit = new Button();
                btnSaveSplit.Text = "✅ Apply Split Payment";
                btnSaveSplit.Location = new Point(20, 268);
                btnSaveSplit.Size = new Size(255, 42);
                Theme.StylePrimaryButton(btnSaveSplit);
                btnSaveSplit.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                dlg.Controls.Add(btnSaveSplit);

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.Location = new Point(285, 268);
                btnCancel.Size = new Size(110, 42);
                Theme.StyleSecondaryButton(btnCancel);
                btnCancel.Click += (s, e) => dlg.DialogResult = DialogResult.Cancel;
                dlg.Controls.Add(btnCancel);

                bool updating = false;

                Action updateStatus = () => {
                    decimal c = 0, o = 0;
                    decimal.TryParse(txtCash.Text.Trim(), out c);
                    decimal.TryParse(txtOnline.Text.Trim(), out o);
                    decimal totalTendered = c + o;
                    decimal diff = grandTotal - totalTendered;

                    if (Math.Abs(diff) < 0.01m)
                    {
                        lblStatus.ForeColor = Theme.Success;
                        lblStatus.Text = $"✅ Exact Match: Rs. {totalTendered:N2} = Rs. {grandTotal:N2}";
                        btnSaveSplit.Enabled = true;
                    }
                    else if (diff > 0)
                    {
                        lblStatus.ForeColor = Theme.Danger;
                        lblStatus.Text = $"⚠️ Underpaid: Rs. {diff:N2} remaining";
                        btnSaveSplit.Enabled = false;
                    }
                    else
                    {
                        lblStatus.ForeColor = Theme.Warning;
                        lblStatus.Text = $"ℹ️ Overpaid: Rs. {Math.Abs(diff):N2} change due";
                        btnSaveSplit.Enabled = true;
                    }
                };

                txtCash.TextChanged += (s, e) => {
                    if (updating) return;
                    updating = true;
                    if (decimal.TryParse(txtCash.Text.Trim(), out decimal c))
                    {
                        decimal o = Math.Max(0, grandTotal - c);
                        txtOnline.Text = o.ToString("0.##");
                    }
                    updating = false;
                    updateStatus();
                };

                txtOnline.TextChanged += (s, e) => {
                    if (updating) return;
                    updating = true;
                    if (decimal.TryParse(txtOnline.Text.Trim(), out decimal o))
                    {
                        decimal c = Math.Max(0, grandTotal - o);
                        txtCash.Text = c.ToString("0.##");
                    }
                    updating = false;
                    updateStatus();
                };

                btnHalf.Click += (s, e) => {
                    decimal half = Math.Round(grandTotal / 2m, 2);
                    updating = true;
                    txtCash.Text = half.ToString("0.##");
                    txtOnline.Text = (grandTotal - half).ToString("0.##");
                    updating = false;
                    updateStatus();
                };

                btnFullCash.Click += (s, e) => {
                    updating = true;
                    txtCash.Text = grandTotal.ToString("0.##");
                    txtOnline.Text = "0";
                    updating = false;
                    updateStatus();
                };

                btnFullOnline.Click += (s, e) => {
                    updating = true;
                    txtCash.Text = "0";
                    txtOnline.Text = grandTotal.ToString("0.##");
                    updating = false;
                    updateStatus();
                };

                // Initial Values
                if (splitCashAmount > 0 || splitOnlineAmount > 0)
                {
                    txtCash.Text = splitCashAmount.ToString("0.##");
                    txtOnline.Text = splitOnlineAmount.ToString("0.##");
                }
                else
                {
                    decimal half = Math.Round(grandTotal / 2m, 2);
                    txtCash.Text = half.ToString("0.##");
                    txtOnline.Text = (grandTotal - half).ToString("0.##");
                }
                updateStatus();

                btnSaveSplit.Click += (s, e) => {
                    decimal c = 0, o = 0;
                    decimal.TryParse(txtCash.Text.Trim(), out c);
                    decimal.TryParse(txtOnline.Text.Trim(), out o);
                    if (c + o <= 0)
                    {
                        MessageBox.Show("Please enter valid positive tender amounts.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    splitCashAmount = c;
                    splitOnlineAmount = o;
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                };

                if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    btnPayAndPrint.Text = $"🖨️  PAY & PRINT ( Split: Cash {splitCashAmount:F0} + Online {splitOnlineAmount:F0} )";
                    return true;
                }
                return false;
            }
        }

        public void ShowDynamicQRPaymentModal()
        {
            if (cartItems.Count == 0)
            {
                MessageBox.Show("Cart is empty! Add services or products before generating QR Code.", "Empty Cart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RecalculateTotals();
            decimal subTotal = 0;
            foreach (var item in cartItems) subTotal += item.Total;

            decimal.TryParse(txtDiscountVal.Text.Trim(), out decimal discVal);
            decimal discountAmt = (comboDiscountType.SelectedItem?.ToString() == "%") ? subTotal * (discVal / 100m) : discVal;
            if (discountAmt > subTotal) discountAmt = subTotal;

            decimal totalTaxable = 0;
            decimal totalCGST = 0;
            decimal totalSGST = 0;
            decimal totalIGST = 0;
            foreach (var item in cartItems)
            {
                totalTaxable += item.TaxableAmount;
                totalCGST += item.CGSTAmount;
                totalSGST += item.SGSTAmount;
                totalIGST += item.IGSTAmount;
            }
            decimal totalTax = totalCGST + totalSGST + totalIGST;
            decimal grandTotal = (!isGSTBillMode || isTaxInclusive) ? Math.Max(0, subTotal - discountAmt) : (totalTaxable + totalTax);

            string upiId = !string.IsNullOrWhiteSpace(salonUPIId) ? salonUPIId : "saloon@okhdfcbank";
            string payee = !string.IsNullOrWhiteSpace(salonUPIName) ? salonUPIName : salonShopName;
            string tempInvRef = (isGSTBillMode ? "INV-" : "RCP-") + DateTime.Now.ToString("yyMMdd-HHmm");

            using (var qrDlg = new QRPaymentDialog(upiId, payee, grandTotal, tempInvRef))
            {
                if (qrDlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    // Direct Complete & Print
                    BtnPayAndPrint_Click(this, EventArgs.Empty);
                }
            }
        }


        private void LoadStaffList()
        {
            try
            {
                staffList.Clear();
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Id, Name, Role FROM Staff WHERE IsActive = 1 ORDER BY Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                staffList.Add(new StaffMember {
                                    Id = Convert.ToInt32(rdr["Id"]),
                                    Name = rdr["Name"].ToString(),
                                    Role = rdr["Role"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadSalonGSTProfile()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 ShopName, Address, Phone, Email, GSTIN, StateName, StateCode, IsTaxInclusive, DefaultBillType, DefaultGSTRate, UPIId, UPIName, AutoShowQROnUPI, PrintQROnReceipt FROM AppProfile", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                salonShopName = rdr["ShopName"]?.ToString() ?? "Glamour Salon & Spa";
                                salonAddress = rdr["Address"]?.ToString() ?? "Kathmandu, Nepal";
                                salonPhone = rdr["Phone"]?.ToString() ?? "+977-1-4200000";
                                salonEmail = rdr["Email"]?.ToString() ?? "contact@merosaloon.com";
                                salonGSTIN = rdr["GSTIN"]?.ToString() ?? "";
                                salonStateName = rdr["StateName"]?.ToString() ?? "Delhi";
                                salonStateCode = rdr["StateCode"]?.ToString() ?? "07";
                                isTaxInclusive = Convert.ToBoolean(rdr["IsTaxInclusive"] != DBNull.Value ? rdr["IsTaxInclusive"] : true);
                                defaultBillType = rdr["DefaultBillType"]?.ToString() ?? "GST";
                                defaultGSTRate = Convert.ToDecimal(rdr["DefaultGSTRate"] != DBNull.Value ? rdr["DefaultGSTRate"] : 18.00m);
                                isGSTBillMode = !defaultBillType.Contains("Non");

                                salonUPIId = rdr["UPIId"] != DBNull.Value ? rdr["UPIId"].ToString() : "";
                                salonUPIName = rdr["UPIName"] != DBNull.Value ? rdr["UPIName"].ToString() : salonShopName;
                                salonAutoShowQR = rdr["AutoShowQROnUPI"] != DBNull.Value ? Convert.ToBoolean(rdr["AutoShowQROnUPI"]) : true;
                                salonPrintQROnReceipt = rdr["PrintQROnReceipt"] != DBNull.Value ? Convert.ToBoolean(rdr["PrintQROnReceipt"]) : true;

                                SetBillMode(isGSTBillMode, false);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void SetBillMode(bool gstMode, bool recalculate = true)
        {
            isGSTBillMode = gstMode;
            if (btnBillModeGST != null && btnBillModeNonGST != null)
            {
                if (isGSTBillMode)
                {
                    btnBillModeGST.BackColor = Theme.Accent;
                    btnBillModeGST.ForeColor = Color.White;
                    btnBillModeNonGST.BackColor = Theme.CardBg;
                    btnBillModeNonGST.ForeColor = Theme.TextMuted;
                }
                else
                {
                    btnBillModeGST.BackColor = Theme.CardBg;
                    btnBillModeGST.ForeColor = Theme.TextMuted;
                    btnBillModeNonGST.BackColor = Theme.Success;
                    btnBillModeNonGST.ForeColor = Color.White;
                }
            }
            if (recalculate)
            {
                RecalculateTotals();
            }
        }

        private void LoadServicesAndProducts()
        {
            try
            {
                allServices.Clear();
                allProducts.Clear();

                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Services
                    using (SqlCommand cmd = new SqlCommand("SELECT Id, Code, ISNULL(SACCode, '999721') AS SACCode, Name, Category, ISNULL(GSTRate, 18.00) AS GSTRate, Price, DurationMinutes FROM Services WHERE IsActive = 1 ORDER BY Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string name = rdr["Name"].ToString();
                                string cat = rdr["Category"].ToString();
                                string emoji = GetCategoryEmoji(cat, name);

                                allServices.Add(new ServiceItemData {
                                    Id = Convert.ToInt32(rdr["Id"]),
                                    Code = rdr["Code"].ToString(),
                                    SACCode = rdr["SACCode"].ToString(),
                                    Name = name,
                                    Category = cat,
                                    GSTRate = Convert.ToDecimal(rdr["GSTRate"]),
                                    Price = Convert.ToDecimal(rdr["Price"]),
                                    DurationMinutes = Convert.ToInt32(rdr["DurationMinutes"]),
                                    IconEmoji = emoji
                                });
                            }
                        }
                    }

                    // Products
                    using (SqlCommand cmd = new SqlCommand("SELECT Id, Code, ISNULL(HSNCode, '3305') AS HSNCode, Name, Category, ISNULL(GSTRate, 18.00) AS GSTRate, SalesPrice, PurchasePrice, Stock FROM Products ORDER BY Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                allProducts.Add(new ProductItemData {
                                    Id = Convert.ToInt32(rdr["Id"]),
                                    Code = rdr["Code"].ToString(),
                                    HSNCode = rdr["HSNCode"].ToString(),
                                    Name = rdr["Name"].ToString(),
                                    Category = rdr["Category"].ToString(),
                                    GSTRate = Convert.ToDecimal(rdr["GSTRate"]),
                                    Price = Convert.ToDecimal(rdr["SalesPrice"]),
                                    CostPrice = Convert.ToDecimal(rdr["PurchasePrice"]),
                                    Stock = Convert.ToInt32(rdr["Stock"]),
                                    IconEmoji = "🧴"
                                });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private string GetCategoryEmoji(string category, string name)
        {
            string s = (category + " " + name).ToLower();
            if (s.Contains("haircut") || s.Contains("hair cut")) return "💇‍♂️";
            if (s.Contains("wash") || s.Contains("shampoo")) return "🚿";
            if (s.Contains("color") || s.Contains("dye")) return "🎨";
            if (s.Contains("spa") || s.Contains("treatment")) return "💆‍♀️";
            if (s.Contains("facial") || s.Contains("skin") || s.Contains("clean up")) return "🧖‍♀️";
            if (s.Contains("beard") || s.Contains("shave")) return "🧔";
            if (s.Contains("nail") || s.Contains("manicure") || s.Contains("pedicure")) return "💅";
            if (s.Contains("massage")) return "💆‍♂️";
            if (s.Contains("wax")) return "🧴";
            if (s.Contains("kid")) return "👦";
            if (s.Contains("package")) return "🎁";
            return "✂️";
        }

        private (string Emoji, Color DarkBg, Color AccentBorder) GetCategoryStyle(string catName)
        {
            string s = catName.ToLower();
            if (catName == "All") return ("🌟", Color.FromArgb(30, 41, 59), Color.FromArgb(148, 163, 184));
            if (s.Contains("hair treatment") || s.Contains("keratin") || s.Contains("spa")) 
                return ("💆‍♀️", Color.FromArgb(42, 36, 26), Color.FromArgb(245, 158, 11));
            if (s.Contains("hair") || s.Contains("cut") || s.Contains("style")) 
                return ("💇‍♀️", Color.FromArgb(30, 36, 56), Color.FromArgb(139, 92, 246));
            if (s.Contains("skin") || s.Contains("facial") || s.Contains("clean")) 
                return ("🧖‍♀️", Color.FromArgb(24, 42, 36), Color.FromArgb(16, 185, 129));
            if (s.Contains("nail") || s.Contains("mani") || s.Contains("pedi")) 
                return ("💅", Color.FromArgb(42, 26, 36), Color.FromArgb(244, 63, 94));
            if (s.Contains("beard") || s.Contains("shave") || s.Contains("mustache")) 
                return ("🧔", Color.FromArgb(22, 40, 46), Color.FromArgb(20, 184, 166));
            if (s.Contains("makeup") || s.Contains("bridal") || s.Contains("cosmetic")) 
                return ("💄", Color.FromArgb(42, 24, 38), Color.FromArgb(236, 72, 153));
            if (s.Contains("massage") || s.Contains("therapy")) 
                return ("💆‍♂️", Color.FromArgb(44, 32, 24), Color.FromArgb(249, 115, 22));
            if (s.Contains("wax") || s.Contains("thread")) 
                return ("🧴", Color.FromArgb(42, 38, 24), Color.FromArgb(234, 179, 8));
            if (s.Contains("body") || s.Contains("scrub")) 
                return ("🪨", Color.FromArgb(28, 34, 48), Color.FromArgb(148, 163, 184));
            if (s.Contains("kid") || s.Contains("child")) 
                return ("👦", Color.FromArgb(22, 36, 52), Color.FromArgb(56, 189, 248));
            if (s.Contains("package") || s.Contains("combo") || s.Contains("offer")) 
                return ("🎁", Color.FromArgb(40, 26, 52), Color.FromArgb(217, 70, 239));
            if (s.Contains("shampoo") || s.Contains("serum") || s.Contains("oil") || s.Contains("cream") || s.Contains("lotion") || s.Contains("product")) 
                return ("🧴", Color.FromArgb(20, 40, 32), Color.FromArgb(16, 185, 129));
            if (s.Contains("color") || s.Contains("dye") || s.Contains("highlight")) 
                return ("🎨", Color.FromArgb(36, 28, 50), Color.FromArgb(168, 85, 247));
            
            // Hash-based diverse colors for custom categories from master
            int hash = Math.Abs(catName.GetHashCode());
            Color[] borders = { 
                Color.FromArgb(139, 92, 246), Color.FromArgb(245, 158, 11), Color.FromArgb(16, 185, 129), 
                Color.FromArgb(244, 63, 94), Color.FromArgb(20, 184, 166), Color.FromArgb(236, 72, 153), 
                Color.FromArgb(56, 189, 248), Color.FromArgb(249, 115, 22), Color.FromArgb(217, 70, 239) 
            };
            Color border = borders[hash % borders.Length];
            Color bg = Color.FromArgb(border.R / 6 + 15, border.G / 6 + 18, border.B / 6 + 25);
            return ("✨", bg, border);
        }

        private void LoadCategories(string mode = "Products")
        {
            flowCategories.Controls.Clear();

            List<string> categoryNames = new List<string>();

            // Always add "All" first
            categoryNames.Add("All");

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // 1. Fetch distinct categories from Products
                    using (SqlCommand cmd = new SqlCommand("SELECT DISTINCT Category FROM Products WHERE Category IS NOT NULL AND Category <> '' ORDER BY Category ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string name = rdr["Category"].ToString().Trim();
                                if (!categoryNames.Exists(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    categoryNames.Add(name);
                                }
                            }
                        }
                    }

                    // 2. Also fetch from Category Master
                    using (SqlCommand cmd = new SqlCommand("SELECT DISTINCT Name FROM Categories WHERE Name IS NOT NULL AND Name <> '' ORDER BY Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string name = rdr["Name"].ToString().Trim();
                                if (!categoryNames.Exists(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    categoryNames.Add(name);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // If empty, supply standard retail product categories
            if (categoryNames.Count <= 1)
            {
                string[] defaults = { "Hair Care", "Skin Care", "Hair Color", "Spa & Oils", "Cosmetics", "Grooming", "Styling Tools", "Others" };
                foreach (var d in defaults)
                {
                    if (!categoryNames.Exists(c => string.Equals(c, d, StringComparison.OrdinalIgnoreCase)))
                        categoryNames.Add(d);
                }
            }

            foreach (var cat in categoryNames)
            {
                var style = GetCategoryStyle(cat);

                Panel card = new Panel();
                card.Size = new Size(100, 68);
                card.Margin = new Padding(4);
                card.BackColor = (selectedCategory.Equals(cat, StringComparison.OrdinalIgnoreCase)) ? Theme.Accent : style.DarkBg;
                card.Cursor = Cursors.Hand;

                card.Paint += (s, e) => {
                    using (Pen p = new Pen((selectedCategory.Equals(cat, StringComparison.OrdinalIgnoreCase)) ? Color.White : style.AccentBorder, (selectedCategory.Equals(cat, StringComparison.OrdinalIgnoreCase)) ? 2 : 1))
                    {
                        e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                    }
                };

                Label lblEmoji = new Label();
                lblEmoji.Text = style.Emoji;
                lblEmoji.Font = new Font("Segoe UI Emoji", 18F, FontStyle.Regular);
                lblEmoji.ForeColor = Color.White;
                lblEmoji.UseCompatibleTextRendering = true;
                lblEmoji.Location = new Point(0, 4);
                lblEmoji.Size = new Size(100, 32);
                lblEmoji.TextAlign = ContentAlignment.MiddleCenter;
                lblEmoji.BackColor = Color.Transparent;
                card.Controls.Add(lblEmoji);

                Label lblName = new Label();
                lblName.Text = (cat == "All") ? "All Items" : cat;
                lblName.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
                lblName.ForeColor = (selectedCategory.Equals(cat, StringComparison.OrdinalIgnoreCase)) ? Theme.TextWhite : Color.FromArgb(241, 245, 249);
                lblName.Location = new Point(0, 38);
                lblName.Size = new Size(100, 24);
                lblName.TextAlign = ContentAlignment.TopCenter;
                lblName.BackColor = Color.Transparent;
                card.Controls.Add(lblName);

                string currentCatName = cat;
                card.Click += (s, e) => ToggleCategory(currentCatName);
                lblEmoji.Click += (s, e) => ToggleCategory(currentCatName);
                lblName.Click += (s, e) => ToggleCategory(currentCatName);

                flowCategories.Controls.Add(card);
            }
        }

        private void ToggleCategory(string catName)
        {
            selectedCategory = (selectedCategory.Equals(catName, StringComparison.OrdinalIgnoreCase)) ? "All" : catName;
            LoadCategories();
            RenderItemsGrid();
        }

        private void RenderItemsGrid(string currentMode = "Products")
        {
            flowItemsGrid.Controls.Clear();
            lblItemsGridTitle.Text = (selectedCategory == "All") ? "Retail Beauty Products" : $"{selectedCategory} Products";

            foreach (var prd in allProducts)
            {
                if (selectedCategory != "All" && !string.Equals(prd.Category, selectedCategory, StringComparison.OrdinalIgnoreCase) && !prd.Category.ToLower().Contains(selectedCategory.ToLower()))
                    continue;

                flowItemsGrid.Controls.Add(CreateProductCard(prd));
            }
        }

        private Panel CreateServiceCard(ServiceItemData srv)
        {
            Panel card = new Panel();
            card.Size = new Size(210, 80);
            card.Margin = new Padding(6);
            card.BackColor = Theme.CardBg;
            card.Cursor = Cursors.Hand;

            card.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.CardBorder, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                }
            };

            // Icon Image / Emoji Container Box
            Panel iconBox = new Panel();
            iconBox.Size = new Size(46, 46);
            iconBox.Location = new Point(10, 16);
            iconBox.BackColor = Color.FromArgb(30, 41, 59); // Slate 800
            iconBox.Paint += (s, e) => {
                using (Pen p = new Pen(Color.FromArgb(51, 65, 85), 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, iconBox.Width - 1, iconBox.Height - 1);
                }
            };

            Label lblIco = new Label();
            lblIco.Text = srv.IconEmoji;
            lblIco.Font = new Font("Segoe UI Emoji", 18F, FontStyle.Regular);
            lblIco.ForeColor = Color.White;
            lblIco.UseCompatibleTextRendering = true;
            lblIco.Dock = DockStyle.Fill;
            lblIco.TextAlign = ContentAlignment.MiddleCenter;
            iconBox.Controls.Add(lblIco);
            card.Controls.Add(iconBox);

            // Title
            Label lblTitle = new Label();
            lblTitle.Text = srv.Name;
            lblTitle.Location = new Point(62, 10);
            lblTitle.Size = new Size(102, 34);
            Theme.StyleLabel(lblTitle, Theme.TextWhite, new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold));
            card.Controls.Add(lblTitle);

            // Price & Duration
            Label lblPrice = new Label();
            lblPrice.Text = $"Rs. {srv.Price:N0} • {srv.DurationMinutes}m";
            lblPrice.Location = new Point(62, 48);
            lblPrice.AutoSize = true;
            Theme.StyleLabel(lblPrice, Theme.Accent, new Font("Segoe UI Semibold", 8F, FontStyle.Bold));
            card.Controls.Add(lblPrice);

            // Add button [+]
            Button btnAdd = new Button();
            btnAdd.Text = "+";
            btnAdd.Size = new Size(30, 30);
            btnAdd.Location = new Point(168, 24);
            Theme.StyleButton(btnAdd, Theme.Accent, Theme.TextWhite);
            btnAdd.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnAdd.Click += (s, e) => AddServiceToCart(srv);
            card.Controls.Add(btnAdd);

            card.Click += (s, e) => AddServiceToCart(srv);
            lblTitle.Click += (s, e) => AddServiceToCart(srv);
            lblPrice.Click += (s, e) => AddServiceToCart(srv);
            iconBox.Click += (s, e) => AddServiceToCart(srv);
            lblIco.Click += (s, e) => AddServiceToCart(srv);

            return card;
        }

        private Panel CreateProductCard(ProductItemData prd)
        {
            Panel card = new Panel();
            card.Size = new Size(210, 80);
            card.Margin = new Padding(6);
            card.BackColor = Theme.CardBg;
            card.Cursor = Cursors.Hand;

            card.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.CardBorder, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                }
            };

            // Icon Image / Emoji Container Box
            Panel iconBox = new Panel();
            iconBox.Size = new Size(46, 46);
            iconBox.Location = new Point(10, 16);
            iconBox.BackColor = Color.FromArgb(20, 36, 30); // Dark emerald tint
            iconBox.Paint += (s, e) => {
                using (Pen p = new Pen(Color.FromArgb(16, 185, 129), 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, iconBox.Width - 1, iconBox.Height - 1);
                }
            };

            Label lblIco = new Label();
            lblIco.Text = prd.IconEmoji;
            lblIco.Font = new Font("Segoe UI Emoji", 18F, FontStyle.Regular);
            lblIco.ForeColor = Color.White;
            lblIco.UseCompatibleTextRendering = true;
            lblIco.Dock = DockStyle.Fill;
            lblIco.TextAlign = ContentAlignment.MiddleCenter;
            iconBox.Controls.Add(lblIco);
            card.Controls.Add(iconBox);

            // Title
            Label lblTitle = new Label();
            lblTitle.Text = prd.Name;
            lblTitle.Location = new Point(62, 12);
            lblTitle.Size = new Size(100, 34);
            Theme.StyleLabel(lblTitle, Theme.TextWhite, new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold));
            card.Controls.Add(lblTitle);

            // Price & Stock
            Label lblPrice = new Label();
            lblPrice.Text = $"Rs. {prd.Price:N0} • ({prd.Stock})";
            lblPrice.Location = new Point(62, 48);
            lblPrice.AutoSize = true;
            Theme.StyleLabel(lblPrice, Theme.Success, new Font("Segoe UI Semibold", 8F, FontStyle.Bold));
            card.Controls.Add(lblPrice);

            // Add button [+]
            Button btnAdd = new Button();
            btnAdd.Text = "+";
            btnAdd.Size = new Size(30, 30);
            btnAdd.Location = new Point(168, 24);
            Theme.StyleButton(btnAdd, Theme.Success, Theme.TextWhite);
            btnAdd.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnAdd.Click += (s, e) => AddProductToCart(prd);
            card.Controls.Add(btnAdd);

            card.Click += (s, e) => AddProductToCart(prd);
            lblTitle.Click += (s, e) => AddProductToCart(prd);
            lblPrice.Click += (s, e) => AddProductToCart(prd);
            iconBox.Click += (s, e) => AddProductToCart(prd);
            lblIco.Click += (s, e) => AddProductToCart(prd);

            return card;
        }

        private void AddServiceToCart(ServiceItemData srv)
        {
            // Default Stylist
            int defStaffId = staffList.Count > 0 ? staffList[0].Id : 0;
            string defStaffName = staffList.Count > 0 ? staffList[0].Name : "-";

            // Check if already in cart
            var existing = cartItems.Find(c => c.ItemType == "Service" && c.ItemId == srv.Id && c.StaffId == defStaffId);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                cartItems.Add(new CartItem {
                    ItemType = "Service",
                    ItemId = srv.Id,
                    Code = srv.Code,
                    HSNSAC = !string.IsNullOrEmpty(srv.SACCode) ? srv.SACCode : "999721",
                    Name = srv.Name,
                    StaffId = defStaffId,
                    StaffName = defStaffName,
                    UnitPrice = srv.Price,
                    Quantity = 1,
                    GSTRate = srv.GSTRate > 0 ? srv.GSTRate : defaultGSTRate,
                    CostPrice = 0,
                    IconEmoji = srv.IconEmoji
                });
            }

            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            UpdateCartUI();
        }

        private void AddProductToCart(ProductItemData prd)
        {
            var existing = cartItems.Find(c => c.ItemType == "Product" && c.ItemId == prd.Id);
            int currentQty = existing != null ? existing.Quantity : 0;

            if (currentQty + 1 > prd.Stock)
            {
                MessageBox.Show($"Only {prd.Stock} unit(s) available in stock!", "Stock Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                cartItems.Add(new CartItem {
                    ItemType = "Product",
                    ItemId = prd.Id,
                    Code = prd.Code,
                    HSNSAC = !string.IsNullOrEmpty(prd.HSNCode) ? prd.HSNCode : "3305",
                    Name = prd.Name,
                    StaffId = 0,
                    StaffName = "-",
                    UnitPrice = prd.Price,
                    Quantity = 1,
                    GSTRate = prd.GSTRate > 0 ? prd.GSTRate : defaultGSTRate,
                    CostPrice = prd.CostPrice,
                    IconEmoji = prd.IconEmoji
                });
            }

            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            UpdateCartUI();
        }

        private void TxtBarcodeScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ProcessBarcodeScan(txtBarcodeScan.Text);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F2)
            {
                txtBarcodeScan?.Focus();
                txtBarcodeScan?.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ProcessBarcodeScan(string rawCode)
        {
            if (string.IsNullOrWhiteSpace(rawCode)) return;

            string code = rawCode.Trim();

            // 1. Check in allProducts by exact code match
            var prd = allProducts.Find(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
            
            // 2. If not matched, try matching by product name
            if (prd == null)
            {
                prd = allProducts.Find(p => string.Equals(p.Name, code, StringComparison.OrdinalIgnoreCase));
            }

            // 3. If still null, try querying the database directly
            if (prd == null)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT TOP 1 Id, Code, Name, Category, SalesPrice, PurchasePrice, Stock 
                            FROM Products 
                            WHERE Code = @c OR Name = @c", conn))
                        {
                            cmd.Parameters.AddWithValue("@c", code);
                            using (SqlDataReader rdr = cmd.ExecuteReader())
                            {
                                if (rdr.Read())
                                {
                                    prd = new ProductItemData
                                    {
                                        Id = Convert.ToInt32(rdr["Id"]),
                                        Code = rdr["Code"].ToString(),
                                        Name = rdr["Name"].ToString(),
                                        Category = rdr["Category"].ToString(),
                                        Price = Convert.ToDecimal(rdr["SalesPrice"]),
                                        CostPrice = Convert.ToDecimal(rdr["PurchasePrice"]),
                                        Stock = Convert.ToInt32(rdr["Stock"]),
                                        IconEmoji = "🧴"
                                    };
                                    allProducts.Add(prd);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (prd != null)
            {
                AddProductToCart(prd);
                txtBarcodeScan.Text = "";
                txtBarcodeScan.Focus();
                return;
            }

            // 4. Also check if it matches a service code/name
            var srv = allServices.Find(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Name, code, StringComparison.OrdinalIgnoreCase));
            if (srv != null)
            {
                MessageBox.Show($"'{srv.Name}' is a Salon Service.\n\nSalon Services cannot be directly sold from the POS screen. Please book and manage services through the Appointments tab to assign stylists, chairs, and booking slots.", "Service Booking Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtBarcodeScan.Text = "";
                txtBarcodeScan.Focus();
                return;
            }

            // 5. If not found, show user friendly alert
            MessageBox.Show($"Product / Barcode '{code}' not found in inventory catalog.", "Barcode Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtBarcodeScan.SelectAll();
            txtBarcodeScan.Focus();
        }

        private void UpdateCartUI()
        {
            flowCartItems.Controls.Clear();
            lblOrderItemsCount.Text = $"Order Items ( {cartItems.Count} )";

            foreach (var item in cartItems)
            {
                Panel row = new Panel();
                row.Size = new Size(345, 56);
                row.Margin = new Padding(0, 0, 0, 6);
                row.BackColor = Theme.InputBg;

                row.Paint += (s, e) => {
                    using (Pen p = new Pen(Theme.CardBorder, 1))
                    {
                        e.Graphics.DrawRectangle(p, 0, 0, row.Width - 1, row.Height - 1);
                    }
                };

                // Thumbnail / Emoji
                Label lblIco = new Label();
                lblIco.Text = item.IconEmoji;
                lblIco.Font = new Font("Segoe UI", 13F);
                lblIco.Location = new Point(6, 12);
                lblIco.Size = new Size(24, 24);
                lblIco.BackColor = Color.Transparent;
                row.Controls.Add(lblIco);

                // Title
                Label lblName = new Label();
                lblName.Text = item.Name;
                lblName.Location = new Point(34, 6);
                lblName.Size = new Size(140, 20);
                lblName.BackColor = Color.Transparent;
                Theme.StyleLabel(lblName, Theme.TextWhite, new Font("Segoe UI Semibold", 8F, FontStyle.Bold));
                row.Controls.Add(lblName);

                // Staff Subtitle (Clickable to switch staff per line item)
                Label lblStaff = new Label();
                if (item.ItemType == "Service")
                {
                    lblStaff.Text = $"✂️ Stylist: {item.StaffName} ▾";
                    lblStaff.Location = new Point(34, 28);
                    lblStaff.Size = new Size(150, 18);
                    lblStaff.BackColor = Color.Transparent;
                    Theme.StyleLabel(lblStaff, Theme.Accent, new Font("Segoe UI Semibold", 8F, FontStyle.Bold));
                    lblStaff.Cursor = Cursors.Hand;
                    lblStaff.Click += (s, e) => ShowStaffSelectMenu(item, lblStaff);
                }
                else
                {
                    lblStaff.Text = $"📦 Product (Qty: {item.Quantity})";
                    lblStaff.Location = new Point(34, 28);
                    lblStaff.Size = new Size(150, 18);
                    lblStaff.BackColor = Color.Transparent;
                    Theme.StyleLabel(lblStaff, Theme.TextMuted, new Font("Segoe UI", 7.5F));
                }
                row.Controls.Add(lblStaff);

                // Delete Button [ 🗑️ ]
                Button btnDel = new Button();
                btnDel.Text = "🗑️";
                btnDel.Size = new Size(22, 22);
                btnDel.Location = new Point(185, 16);
                btnDel.FlatStyle = FlatStyle.Flat;
                btnDel.FlatAppearance.BorderSize = 0;
                btnDel.BackColor = Color.Transparent;
                btnDel.Font = new Font("Segoe UI", 7F);
                btnDel.Click += (s, e) => { cartItems.Remove(item); UpdateCartUI(); };
                row.Controls.Add(btnDel);

                // Minus [-]
                Button btnMinus = new Button();
                btnMinus.Text = "-";
                btnMinus.Size = new Size(20, 22);
                btnMinus.Location = new Point(210, 16);
                Theme.StyleButton(btnMinus, Theme.CardBg, Theme.TextWhite);
                btnMinus.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                btnMinus.Click += (s, e) => {
                    if (item.Quantity > 1) item.Quantity--;
                    else cartItems.Remove(item);
                    UpdateCartUI();
                };
                row.Controls.Add(btnMinus);

                // Qty Display
                Label lblQty = new Label();
                lblQty.Text = item.Quantity.ToString();
                lblQty.Location = new Point(232, 18);
                lblQty.Size = new Size(20, 18);
                lblQty.TextAlign = ContentAlignment.MiddleCenter;
                lblQty.BackColor = Color.Transparent;
                Theme.StyleLabel(lblQty, Theme.TextWhite, new Font("Segoe UI Semibold", 8F, FontStyle.Bold));
                row.Controls.Add(lblQty);

                // Plus [+]
                Button btnPlus = new Button();
                btnPlus.Text = "+";
                btnPlus.Size = new Size(20, 22);
                btnPlus.Location = new Point(254, 16);
                Theme.StyleButton(btnPlus, Theme.CardBg, Theme.TextWhite);
                btnPlus.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                btnPlus.Click += (s, e) => { item.Quantity++; UpdateCartUI(); };
                row.Controls.Add(btnPlus);

                // Total Price (Clickable to Edit Price / Add Extra Amount)
                Label lblTotal = new Label();
                lblTotal.Text = $"Rs. {item.Total:N0} ✏️";
                lblTotal.Location = new Point(268, 17);
                lblTotal.Size = new Size(74, 20);
                lblTotal.TextAlign = ContentAlignment.MiddleRight;
                lblTotal.BackColor = Color.Transparent;
                Theme.StyleLabel(lblTotal, Theme.Accent, new Font("Segoe UI Semibold", 8F, FontStyle.Bold));
                lblTotal.Cursor = Cursors.Hand;
                ToolTip tip = new ToolTip();
                tip.SetToolTip(lblTotal, "Click to edit amount or add extra charge");
                lblTotal.Click += (s, e) => ShowEditPriceDialog(item);
                row.Controls.Add(lblTotal);

                flowCartItems.Controls.Add(row);
            }

            RecalculateTotals();
        }

        private void ShowEditPriceDialog(CartItem item)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = $"Edit Rate / Add Extra: {item.Name}";
                dlg.Size = new Size(350, 240);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Theme.Primary;

                Label lblHeader = new Label();
                lblHeader.Text = $"💇 {item.Name}\nCurrent Rate: Rs. {item.UnitPrice:N2} | Qty: {item.Quantity}";
                lblHeader.Location = new Point(20, 15);
                lblHeader.Size = new Size(300, 36);
                Theme.StyleLabel(lblHeader, Theme.TextLight, new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold));
                dlg.Controls.Add(lblHeader);

                Label lblPrompt = new Label();
                lblPrompt.Text = "Enter New Unit Price / Updated Amount (Rs.):";
                lblPrompt.Location = new Point(20, 58);
                lblPrompt.Size = new Size(300, 18);
                Theme.StyleLabel(lblPrompt, Theme.TextMuted, Theme.MainFont);
                dlg.Controls.Add(lblPrompt);

                TextBox txtNewPrice = new TextBox();
                txtNewPrice.Location = new Point(20, 80);
                txtNewPrice.Size = new Size(295, 26);
                txtNewPrice.Text = item.UnitPrice.ToString("0.##");
                Theme.StyleTextBox(txtNewPrice);
                dlg.Controls.Add(txtNewPrice);

                // Quick add buttons (+50, +100, +200, +500)
                FlowLayoutPanel pnlQuick = new FlowLayoutPanel();
                pnlQuick.Location = new Point(20, 114);
                pnlQuick.Size = new Size(300, 30);
                pnlQuick.BackColor = Color.Transparent;

                int[] quickAdds = { 50, 100, 200, 500 };
                foreach (int add in quickAdds)
                {
                    Button btnQ = new Button();
                    btnQ.Text = $"+{add}";
                    btnQ.Size = new Size(62, 24);
                    btnQ.Margin = new Padding(0, 0, 8, 0);
                    Theme.StyleSecondaryButton(btnQ);
                    btnQ.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    btnQ.Click += (s, e) => {
                        if (decimal.TryParse(txtNewPrice.Text.Trim(), out decimal curVal))
                        {
                            txtNewPrice.Text = (curVal + add).ToString("0.##");
                        }
                    };
                    pnlQuick.Controls.Add(btnQ);
                }
                dlg.Controls.Add(pnlQuick);

                Button btnSave = new Button();
                btnSave.Text = "✔ Update Amount";
                btnSave.Size = new Size(140, 32);
                btnSave.Location = new Point(20, 155);
                Theme.StyleSuccessButton(btnSave);
                btnSave.Click += (s, e) => {
                    if (decimal.TryParse(txtNewPrice.Text.Trim(), out decimal newPrice) && newPrice >= 0)
                    {
                        item.UnitPrice = newPrice;
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid non-negative amount.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                dlg.Controls.Add(btnSave);

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.Size = new Size(100, 32);
                btnCancel.Location = new Point(170, 155);
                Theme.StyleSecondaryButton(btnCancel);
                btnCancel.Click += (s, e) => dlg.Close();
                dlg.Controls.Add(btnCancel);

                dlg.AcceptButton = btnSave;
                txtNewPrice.SelectAll();

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    UpdateCartUI();
                }
            }
        }

        private void ShowAddExtraChargeDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Add Custom / Extra Service Charge";
                dlg.Size = new Size(360, 280);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Theme.Primary;

                Label lblPrompt = new Label();
                lblPrompt.Text = "Extra Service Name / Description:";
                lblPrompt.Location = new Point(20, 15);
                lblPrompt.Size = new Size(300, 18);
                Theme.StyleLabel(lblPrompt, Theme.TextMuted, Theme.MainFont);
                dlg.Controls.Add(lblPrompt);

                TextBox txtName = new TextBox();
                txtName.Location = new Point(20, 35);
                txtName.Size = new Size(300, 26);
                txtName.Text = "Extra Service Charge";
                Theme.StyleTextBox(txtName);
                dlg.Controls.Add(txtName);

                Label lblAmt = new Label();
                lblAmt.Text = "Extra Amount (Rs.):";
                lblAmt.Location = new Point(20, 70);
                lblAmt.Size = new Size(300, 18);
                Theme.StyleLabel(lblAmt, Theme.TextMuted, Theme.MainFont);
                dlg.Controls.Add(lblAmt);

                TextBox txtAmt = new TextBox();
                txtAmt.Location = new Point(20, 90);
                txtAmt.Size = new Size(300, 26);
                txtAmt.Text = "100";
                Theme.StyleTextBox(txtAmt);
                dlg.Controls.Add(txtAmt);

                Label lblSt = new Label();
                lblSt.Text = "Assigned Stylist:";
                lblSt.Location = new Point(20, 125);
                lblSt.Size = new Size(300, 18);
                Theme.StyleLabel(lblSt, Theme.TextMuted, Theme.MainFont);
                dlg.Controls.Add(lblSt);

                ComboBox cbStaff = new ComboBox();
                cbStaff.Location = new Point(20, 145);
                cbStaff.Size = new Size(300, 26);
                cbStaff.DropDownStyle = ComboBoxStyle.DropDownList;
                Theme.StyleComboBox(cbStaff);
                cbStaff.Items.Add(new ComboBoxItem { Id = 0, Display = "None / Salon Default" });
                foreach (var st in staffList)
                {
                    cbStaff.Items.Add(new ComboBoxItem { Id = st.Id, Display = $"{st.Name} ({st.Role})" });
                }
                if (cbStaff.Items.Count > 0) cbStaff.SelectedIndex = 0;
                dlg.Controls.Add(cbStaff);

                Button btnAdd = new Button();
                btnAdd.Text = "➕ Add to Bill";
                btnAdd.Size = new Size(130, 32);
                btnAdd.Location = new Point(20, 190);
                Theme.StyleSuccessButton(btnAdd);
                btnAdd.Click += (s, e) => {
                    string desc = txtName.Text.Trim();
                    if (string.IsNullOrEmpty(desc)) desc = "Extra Service Charge";

                    if (decimal.TryParse(txtAmt.Text.Trim(), out decimal amt) && amt > 0)
                    {
                        int stId = 0;
                        string stName = "-";
                        if (cbStaff.SelectedItem is ComboBoxItem cbi && cbi.Id > 0)
                        {
                            stId = cbi.Id;
                            stName = cbi.Display;
                        }

                        cartItems.Add(new CartItem {
                            ItemType = "Service",
                            ItemId = 0,
                            Code = "EXTRA",
                            HSNSAC = "999721",
                            Name = desc,
                            StaffId = stId,
                            StaffName = stName,
                            UnitPrice = amt,
                            Quantity = 1,
                            GSTRate = defaultGSTRate,
                            CostPrice = 0,
                            IconEmoji = "✨"
                        });

                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid extra amount greater than 0.", "Invalid Amount", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                dlg.Controls.Add(btnAdd);

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.Size = new Size(100, 32);
                btnCancel.Location = new Point(160, 190);
                Theme.StyleSecondaryButton(btnCancel);
                btnCancel.Click += (s, e) => dlg.Close();
                dlg.Controls.Add(btnCancel);

                dlg.AcceptButton = btnAdd;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    UpdateCartUI();
                }
            }
        }

        private void ShowStaffSelectMenu(CartItem item, Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Theme.Secondary;
            menu.ForeColor = Theme.TextLight;
            menu.Font = Theme.MainFont;
            
            foreach (var st in staffList)
            {
                var menuItem = menu.Items.Add($"✂️  {st.Name}  ({st.Role})");
                menuItem.ForeColor = Theme.TextLight;
                menuItem.Click += (s, e) => {
                    item.StaffId = st.Id;
                    item.StaffName = st.Name;
                    UpdateCartUI();
                };
            }
            menu.Show(anchor, new Point(0, anchor.Height + 2));
        }

        private void RecalculateTotals()
        {
            decimal subTotal = 0;
            foreach (var item in cartItems)
            {
                subTotal += item.Total;
            }
            lblSubTotalVal.Text = $"Rs. {subTotal:N2}";

            decimal.TryParse(txtDiscountVal.Text.Trim(), out decimal discVal);
            decimal discountAmt = 0;
            if (comboDiscountType.SelectedItem?.ToString() == "%")
            {
                discountAmt = subTotal * (discVal / 100m);
            }
            else
            {
                discountAmt = discVal;
            }
            if (discountAmt > subTotal) discountAmt = subTotal;
            lblDiscountCalculated.Text = $"- Rs. {discountAmt:N2}";

            if (!isGSTBillMode)
            {
                // NON-GST BILL MODE
                decimal netPayable = Math.Max(0, subTotal - discountAmt);
                lblTaxableTitle.Text = "Net Amount";
                lblTaxableVal.Text = $"Rs. {netPayable:N2}";
                lblTaxBreakdownTitle.Text = "Tax (Non-GST)";
                lblTaxCalculated.Text = "Rs. 0.00";
                lblTaxCalculated.ForeColor = Theme.TextMuted;
                lblTotalPayableVal.Text = $"Rs. {netPayable:N2}";
                btnPayAndPrint.Text = $"🖨️  PAY & PRINT ( Rs. {netPayable:N2} )";

                foreach (var item in cartItems)
                {
                    decimal lineRatio = subTotal > 0 ? (item.Total / subTotal) : 0;
                    decimal lineDisc = discountAmt * lineRatio;
                    item.TaxableAmount = Math.Max(0, item.Total - lineDisc);
                    item.CGSTAmount = 0;
                    item.SGSTAmount = 0;
                    item.IGSTAmount = 0;
                }
                return;
            }

            // GST BILL MODE (Indian Standard Rules)
            bool isInterState = !string.IsNullOrEmpty(currentCustomerStateCode) &&
                                !string.IsNullOrEmpty(salonStateCode) &&
                                !string.Equals(currentCustomerStateCode, salonStateCode, StringComparison.OrdinalIgnoreCase);

            decimal totalTaxable = 0;
            decimal totalCGST = 0;
            decimal totalSGST = 0;
            decimal totalIGST = 0;

            foreach (var item in cartItems)
            {
                decimal lineRatio = subTotal > 0 ? (item.Total / subTotal) : 0;
                decimal lineDisc = discountAmt * lineRatio;
                decimal netLine = Math.Max(0, item.Total - lineDisc);
                decimal rate = item.GSTRate >= 0 ? item.GSTRate : defaultGSTRate;

                decimal lineTaxable = 0;
                decimal lineTax = 0;

                if (isTaxInclusive)
                {
                    lineTaxable = Math.Round(netLine * 100m / (100m + rate), 2);
                    lineTax = netLine - lineTaxable;
                }
                else
                {
                    lineTaxable = netLine;
                    lineTax = Math.Round(netLine * (rate / 100m), 2);
                }

                item.TaxableAmount = lineTaxable;

                if (isInterState)
                {
                    item.IGSTAmount = lineTax;
                    item.CGSTAmount = 0;
                    item.SGSTAmount = 0;
                    totalIGST += lineTax;
                }
                else
                {
                    decimal halfTax = Math.Round(lineTax / 2m, 2);
                    item.CGSTAmount = halfTax;
                    item.SGSTAmount = lineTax - halfTax;
                    item.IGSTAmount = 0;
                    totalCGST += item.CGSTAmount;
                    totalSGST += item.SGSTAmount;
                }

                totalTaxable += lineTaxable;
            }

            decimal totalTax = totalCGST + totalSGST + totalIGST;
            decimal grandTotal = isTaxInclusive ? Math.Max(0, subTotal - discountAmt) : (totalTaxable + totalTax);

            lblTaxableTitle.Text = "Taxable Value";
            lblTaxableVal.Text = $"Rs. {totalTaxable:N2}";

            if (isInterState)
            {
                lblTaxBreakdownTitle.Text = "IGST Tax";
                lblTaxCalculated.Text = $"Rs. {totalIGST:N2}";
            }
            else
            {
                lblTaxBreakdownTitle.Text = "CGST + SGST";
                lblTaxCalculated.Text = $"Rs. {totalTax:N2}";
            }
            lblTaxCalculated.ForeColor = Theme.Accent;

            lblTotalPayableVal.Text = $"Rs. {grandTotal:N2}";
            btnPayAndPrint.Text = $"🖨️  PAY & PRINT ( Rs. {grandTotal:N2} )";
        }

        private void BtnPayAndPrint_Click(object sender, EventArgs e)
        {
            if (cartItems.Count == 0)
            {
                MessageBox.Show("Order cart is empty! Please add services or retail products.", "Empty Cart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RecalculateTotals();

            decimal subTotal = 0;
            foreach (var item in cartItems) subTotal += item.Total;

            decimal.TryParse(txtDiscountVal.Text.Trim(), out decimal discVal);
            decimal discountAmt = (comboDiscountType.SelectedItem?.ToString() == "%") ? subTotal * (discVal / 100m) : discVal;
            if (discountAmt > subTotal) discountAmt = subTotal;

            decimal totalTaxable = 0;
            decimal totalCGST = 0;
            decimal totalSGST = 0;
            decimal totalIGST = 0;
            foreach (var item in cartItems)
            {
                totalTaxable += item.TaxableAmount;
                totalCGST += item.CGSTAmount;
                totalSGST += item.SGSTAmount;
                totalIGST += item.IGSTAmount;
            }
            decimal totalTax = totalCGST + totalSGST + totalIGST;
            decimal grandTotal = (!isGSTBillMode || isTaxInclusive) ? Math.Max(0, subTotal - discountAmt) : (totalTaxable + totalTax);

            decimal cashPortion = 0.00m;
            decimal onlinePortion = 0.00m;

            if (selectedPaymentMethod == "Split")
            {
                if (splitCashAmount + splitOnlineAmount <= 0 || Math.Abs((splitCashAmount + splitOnlineAmount) - grandTotal) > 0.05m)
                {
                    if (!ShowSplitPaymentModal(true))
                    {
                        return; // user cancelled split dialog
                    }
                }
                cashPortion = splitCashAmount;
                onlinePortion = splitOnlineAmount;
            }
            else if (selectedPaymentMethod == "Cash")
            {
                cashPortion = grandTotal;
                onlinePortion = 0.00m;
            }
            else if (selectedPaymentMethod == "Due / Wallet")
            {
                cashPortion = 0.00m;
                onlinePortion = 0.00m;
            }
            else
            {
                // QR Pay / UPI, Card, etc.
                cashPortion = 0.00m;
                onlinePortion = grandTotal;
            }

            bool isInterState = !string.IsNullOrEmpty(currentCustomerStateCode) &&
                                !string.IsNullOrEmpty(salonStateCode) &&
                                !string.Equals(currentCustomerStateCode, salonStateCode, StringComparison.OrdinalIgnoreCase);

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            if (editingSaleId > 0)
                            {
                                // ==============================================================
                                // IN-PLACE INVOICE ADJUSTMENT: UPDATE EXISTING SAVED INVOICE
                                // ==============================================================
                                
                                // 1. Restore product stock for previous items in this invoice
                                using (SqlCommand cmdRestoreStock = new SqlCommand(@"
                                    UPDATE p
                                    SET p.Stock = p.Stock + sd.Quantity
                                    FROM Products p
                                    INNER JOIN SaleDetails sd ON p.Id = sd.ProductId
                                    WHERE sd.SaleId = @saleId AND sd.ItemType = 'Product' AND sd.ProductId IS NOT NULL", conn, trans))
                                {
                                    cmdRestoreStock.Parameters.AddWithValue("@saleId", editingSaleId);
                                    cmdRestoreStock.ExecuteNonQuery();
                                }

                                // 2. Delete old details
                                using (SqlCommand cmdDel = new SqlCommand("DELETE FROM SaleDetails WHERE SaleId = @saleId", conn, trans))
                                {
                                    cmdDel.Parameters.AddWithValue("@saleId", editingSaleId);
                                    cmdDel.ExecuteNonQuery();
                                }

                                // 3. Update Sales Header in-place (preserving original InvoiceNumber & SaleDate)
                                using (SqlCommand cmdUpdSale = new SqlCommand(@"
                                    UPDATE Sales SET
                                        CustomerId = @cust,
                                        SubTotal = @sub,
                                        Discount = @disc,
                                        Tax = @tx,
                                        GrandTotal = @grand,
                                        AmountPaid = @paid,
                                        DueAmount = 0.00,
                                        PaymentMethod = @payMode,
                                        IsGSTBill = @isGst,
                                        TaxableAmount = @taxable,
                                        CGSTAmount = @cgst,
                                        SGSTAmount = @sgst,
                                        IGSTAmount = @igst,
                                        CustomerGSTIN = @custGst,
                                        PlaceOfSupply = @pos,
                                        IsInterState = @isInter,
                                        CashAmount = @cashAmt,
                                        OnlineAmount = @onlineAmt
                                    WHERE Id = @saleId", conn, trans))
                                {
                                    cmdUpdSale.Parameters.AddWithValue("@saleId", editingSaleId);
                                    cmdUpdSale.Parameters.AddWithValue("@cust", currentCustomerId);
                                    cmdUpdSale.Parameters.AddWithValue("@sub", subTotal);
                                    cmdUpdSale.Parameters.AddWithValue("@disc", discountAmt);
                                    cmdUpdSale.Parameters.AddWithValue("@tx", isGSTBillMode ? totalTax : 0.00m);
                                    cmdUpdSale.Parameters.AddWithValue("@grand", grandTotal);
                                    cmdUpdSale.Parameters.AddWithValue("@paid", grandTotal);
                                    cmdUpdSale.Parameters.AddWithValue("@payMode", selectedPaymentMethod);
                                    cmdUpdSale.Parameters.AddWithValue("@isGst", isGSTBillMode);
                                    cmdUpdSale.Parameters.AddWithValue("@taxable", totalTaxable);
                                    cmdUpdSale.Parameters.AddWithValue("@cgst", isGSTBillMode ? totalCGST : 0.00m);
                                    cmdUpdSale.Parameters.AddWithValue("@sgst", isGSTBillMode ? totalSGST : 0.00m);
                                    cmdUpdSale.Parameters.AddWithValue("@igst", isGSTBillMode ? totalIGST : 0.00m);
                                    cmdUpdSale.Parameters.AddWithValue("@custGst", string.IsNullOrEmpty(currentCustomerGSTIN) ? DBNull.Value : (object)currentCustomerGSTIN);
                                    cmdUpdSale.Parameters.AddWithValue("@pos", currentCustomerStateName);
                                    cmdUpdSale.Parameters.AddWithValue("@isInter", isInterState);
                                    cmdUpdSale.Parameters.AddWithValue("@cashAmt", cashPortion);
                                    cmdUpdSale.Parameters.AddWithValue("@onlineAmt", onlinePortion);
                                    cmdUpdSale.ExecuteNonQuery();
                                }

                                // 4. Insert updated SaleDetails
                                foreach (var item in cartItems)
                                {
                                    if (item.ItemType == "Service")
                                    {
                                        using (SqlCommand cmd = new SqlCommand(@"
                                            INSERT INTO SaleDetails (
                                                SaleId, ItemType, ProductId, ServiceId, StaffId, Quantity, UnitPrice, Total, PurchaseCostAtSale,
                                                HSNSAC, GSTRate, TaxableAmount, CGSTAmount, SGSTAmount, IGSTAmount
                                            )
                                            VALUES (
                                                @saleId, 'Service', NULL, @srvId, @staffId, @qty, @price, @tot, 0.00,
                                                @hsn, @gstRate, @taxable, @cgst, @sgst, @igst
                                            )", conn, trans))
                                        {
                                            cmd.Parameters.AddWithValue("@saleId", editingSaleId);
                                            cmd.Parameters.AddWithValue("@srvId", item.ItemId);
                                            cmd.Parameters.AddWithValue("@staffId", item.StaffId > 0 ? (object)item.StaffId : DBNull.Value);
                                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                                            cmd.Parameters.AddWithValue("@price", item.UnitPrice);
                                            cmd.Parameters.AddWithValue("@tot", item.Total);
                                            cmd.Parameters.AddWithValue("@hsn", item.HSNSAC ?? "999721");
                                            cmd.Parameters.AddWithValue("@gstRate", isGSTBillMode ? item.GSTRate : 0.00m);
                                            cmd.Parameters.AddWithValue("@taxable", item.TaxableAmount);
                                            cmd.Parameters.AddWithValue("@cgst", isGSTBillMode ? item.CGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@sgst", isGSTBillMode ? item.SGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@igst", isGSTBillMode ? item.IGSTAmount : 0.00m);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        using (SqlCommand cmd = new SqlCommand(@"
                                            INSERT INTO SaleDetails (
                                                SaleId, ItemType, ProductId, ServiceId, StaffId, Quantity, UnitPrice, Total, PurchaseCostAtSale,
                                                HSNSAC, GSTRate, TaxableAmount, CGSTAmount, SGSTAmount, IGSTAmount
                                            )
                                            VALUES (
                                                @saleId, 'Product', @prodId, NULL, NULL, @qty, @price, @tot, @cost,
                                                @hsn, @gstRate, @taxable, @cgst, @sgst, @igst
                                            )", conn, trans))
                                        {
                                            cmd.Parameters.AddWithValue("@saleId", editingSaleId);
                                            cmd.Parameters.AddWithValue("@prodId", item.ItemId);
                                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                                            cmd.Parameters.AddWithValue("@price", item.UnitPrice);
                                            cmd.Parameters.AddWithValue("@tot", item.Total);
                                            cmd.Parameters.AddWithValue("@cost", item.CostPrice);
                                            cmd.Parameters.AddWithValue("@hsn", item.HSNSAC ?? "3305");
                                            cmd.Parameters.AddWithValue("@gstRate", isGSTBillMode ? item.GSTRate : 0.00m);
                                            cmd.Parameters.AddWithValue("@taxable", item.TaxableAmount);
                                            cmd.Parameters.AddWithValue("@cgst", isGSTBillMode ? item.CGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@sgst", isGSTBillMode ? item.SGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@igst", isGSTBillMode ? item.IGSTAmount : 0.00m);
                                            cmd.ExecuteNonQuery();
                                        }

                                        using (SqlCommand cmd = new SqlCommand("UPDATE Products SET Stock = Stock - @qty WHERE Id = @prodId", conn, trans))
                                        {
                                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                                            cmd.Parameters.AddWithValue("@prodId", item.ItemId);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }

                                if (currentAppointmentId > 0)
                                {
                                    try
                                    {
                                        int primaryStaffId = 0;
                                        var srvIdsList = new System.Collections.Generic.List<int>();
                                        var srvNamesList = new System.Collections.Generic.List<string>();
                                        var srvStaffIdsList = new System.Collections.Generic.List<string>();

                                        foreach (var item in cartItems)
                                        {
                                            if (item.ItemType == "Service")
                                            {
                                                if (primaryStaffId == 0 && item.StaffId > 0) primaryStaffId = item.StaffId;
                                                srvIdsList.Add(item.ItemId);
                                                srvNamesList.Add($"{item.Name}" + (!string.IsNullOrEmpty(item.StaffName) ? $" ({item.StaffName})" : ""));
                                                srvStaffIdsList.Add($"{item.ItemId}:{item.StaffId}");
                                            }
                                        }

                                        string srvIdsStr = string.Join(",", srvIdsList);
                                        string srvNamesStr = string.Join(" ➜ ", srvNamesList);
                                        string srvStaffIdsStr = string.Join(",", srvStaffIdsList);

                                        using (SqlCommand cmdAppt = new SqlCommand(@"
                                            UPDATE Appointments SET 
                                                Status = 'Billed', 
                                                SaleId = @saleId,
                                                CustomerId = @cust,
                                                StaffId = CASE WHEN @stId > 0 THEN @stId ELSE StaffId END,
                                                ServiceIds = CASE WHEN LEN(@sIds) > 0 THEN @sIds ELSE ServiceIds END,
                                                ServiceNames = CASE WHEN LEN(@sNames) > 0 THEN @sNames ELSE ServiceNames END,
                                                ServiceStaffIds = CASE WHEN LEN(@sStaffIds) > 0 THEN @sStaffIds ELSE ServiceStaffIds END
                                            WHERE Id = @apptId", conn, trans))
                                        {
                                            cmdAppt.Parameters.AddWithValue("@saleId", editingSaleId);
                                            cmdAppt.Parameters.AddWithValue("@cust", currentCustomerId);
                                            cmdAppt.Parameters.AddWithValue("@stId", primaryStaffId);
                                            cmdAppt.Parameters.AddWithValue("@sIds", srvIdsStr);
                                            cmdAppt.Parameters.AddWithValue("@sNames", srvNamesStr);
                                            cmdAppt.Parameters.AddWithValue("@sStaffIds", srvStaffIdsStr);
                                            cmdAppt.Parameters.AddWithValue("@apptId", currentAppointmentId);
                                            cmdAppt.ExecuteNonQuery();
                                        }
                                        using (SqlCommand cmdSalesAppt = new SqlCommand("UPDATE Sales SET AppointmentId = @apptId WHERE Id = @saleId", conn, trans))
                                        {
                                            cmdSalesAppt.Parameters.AddWithValue("@apptId", currentAppointmentId);
                                            cmdSalesAppt.Parameters.AddWithValue("@saleId", editingSaleId);
                                            cmdSalesAppt.ExecuteNonQuery();
                                        }
                                    }
                                    catch { }
                                }

                                trans.Commit();
                                lastSaleId = editingSaleId;
                                string savedInvNum = editingInvoiceNumber;

                                MessageBox.Show($"Saved invoice #{savedInvNum} has been updated & adjusted successfully!\nTotal: Rs. {grandTotal:N2}", "Invoice Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                try
                                {
                                    ThermalReceiptPrinter.ShowPreview(lastSaleId);
                                }
                                catch
                                {
                                    ThermalReceiptPrinter.Print(lastSaleId);
                                }

                                ExitEditMode();
                            }
                            else
                            {
                                EnsureWalkInCustomer(conn, trans);

                                // ==============================================================
                                // NEW SALE INVOICE CREATION
                                // ==============================================================
                                string prefix = isGSTBillMode ? "INV-" : "RCP-";
                                string invoiceNum = prefix + DateTime.Now.ToString("yyMMdd-HHmmss");

                                // 1. Sales Header
                                int newSaleId = 0;
                                using (SqlCommand cmd = new SqlCommand(@"
                                    INSERT INTO Sales (
                                        InvoiceNumber, CustomerId, SaleDate, SubTotal, Discount, Tax, GrandTotal, 
                                        AmountPaid, DueAmount, PaymentMethod, CreatedBy,
                                        IsGSTBill, TaxableAmount, CGSTAmount, SGSTAmount, IGSTAmount, 
                                        CustomerGSTIN, PlaceOfSupply, IsInterState,
                                        CashAmount, OnlineAmount
                                    )
                                    OUTPUT INSERTED.Id
                                    VALUES (
                                        @inv, @cust, GETDATE(), @sub, @disc, @tx, @grand, 
                                        @paid, 0.00, @payMode, @user,
                                        @isGst, @taxable, @cgst, @sgst, @igst,
                                        @custGst, @pos, @isInter,
                                        @cashAmt, @onlineAmt
                                    )", conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@inv", invoiceNum);
                                    cmd.Parameters.AddWithValue("@cust", currentCustomerId);
                                    cmd.Parameters.AddWithValue("@sub", subTotal);
                                    cmd.Parameters.AddWithValue("@disc", discountAmt);
                                    cmd.Parameters.AddWithValue("@tx", isGSTBillMode ? totalTax : 0.00m);
                                    cmd.Parameters.AddWithValue("@grand", grandTotal);
                                    cmd.Parameters.AddWithValue("@paid", grandTotal);
                                    cmd.Parameters.AddWithValue("@payMode", selectedPaymentMethod);
                                    cmd.Parameters.AddWithValue("@user", Session.UserId > 0 ? (object)Session.UserId : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@isGst", isGSTBillMode);
                                    cmd.Parameters.AddWithValue("@taxable", totalTaxable);
                                    cmd.Parameters.AddWithValue("@cgst", isGSTBillMode ? totalCGST : 0.00m);
                                    cmd.Parameters.AddWithValue("@sgst", isGSTBillMode ? totalSGST : 0.00m);
                                    cmd.Parameters.AddWithValue("@igst", isGSTBillMode ? totalIGST : 0.00m);
                                    cmd.Parameters.AddWithValue("@custGst", string.IsNullOrEmpty(currentCustomerGSTIN) ? DBNull.Value : (object)currentCustomerGSTIN);
                                    cmd.Parameters.AddWithValue("@pos", currentCustomerStateName);
                                    cmd.Parameters.AddWithValue("@isInter", isInterState);
                                    cmd.Parameters.AddWithValue("@cashAmt", cashPortion);
                                    cmd.Parameters.AddWithValue("@onlineAmt", onlinePortion);
                                    newSaleId = (int)cmd.ExecuteScalar();
                                }

                                // 2. Sale Details
                                foreach (var item in cartItems)
                                {
                                    if (item.ItemType == "Service")
                                    {
                                        using (SqlCommand cmd = new SqlCommand(@"
                                            INSERT INTO SaleDetails (
                                                SaleId, ItemType, ProductId, ServiceId, StaffId, Quantity, UnitPrice, Total, PurchaseCostAtSale,
                                                HSNSAC, GSTRate, TaxableAmount, CGSTAmount, SGSTAmount, IGSTAmount
                                            )
                                            VALUES (
                                                @saleId, 'Service', NULL, @srvId, @staffId, @qty, @price, @tot, 0.00,
                                                @hsn, @gstRate, @taxable, @cgst, @sgst, @igst
                                            )", conn, trans))
                                        {
                                            cmd.Parameters.AddWithValue("@saleId", newSaleId);
                                            cmd.Parameters.AddWithValue("@srvId", item.ItemId);
                                            cmd.Parameters.AddWithValue("@staffId", item.StaffId > 0 ? (object)item.StaffId : DBNull.Value);
                                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                                            cmd.Parameters.AddWithValue("@price", item.UnitPrice);
                                            cmd.Parameters.AddWithValue("@tot", item.Total);
                                            cmd.Parameters.AddWithValue("@hsn", item.HSNSAC ?? "999721");
                                            cmd.Parameters.AddWithValue("@gstRate", isGSTBillMode ? item.GSTRate : 0.00m);
                                            cmd.Parameters.AddWithValue("@taxable", item.TaxableAmount);
                                            cmd.Parameters.AddWithValue("@cgst", isGSTBillMode ? item.CGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@sgst", isGSTBillMode ? item.SGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@igst", isGSTBillMode ? item.IGSTAmount : 0.00m);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        using (SqlCommand cmd = new SqlCommand(@"
                                            INSERT INTO SaleDetails (
                                                SaleId, ItemType, ProductId, ServiceId, StaffId, Quantity, UnitPrice, Total, PurchaseCostAtSale,
                                                HSNSAC, GSTRate, TaxableAmount, CGSTAmount, SGSTAmount, IGSTAmount
                                            )
                                            VALUES (
                                                @saleId, 'Product', @prodId, NULL, NULL, @qty, @price, @tot, @cost,
                                                @hsn, @gstRate, @taxable, @cgst, @sgst, @igst
                                            )", conn, trans))
                                        {
                                            cmd.Parameters.AddWithValue("@saleId", newSaleId);
                                            cmd.Parameters.AddWithValue("@prodId", item.ItemId);
                                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                                            cmd.Parameters.AddWithValue("@price", item.UnitPrice);
                                            cmd.Parameters.AddWithValue("@tot", item.Total);
                                            cmd.Parameters.AddWithValue("@cost", item.CostPrice);
                                            cmd.Parameters.AddWithValue("@hsn", item.HSNSAC ?? "3305");
                                            cmd.Parameters.AddWithValue("@gstRate", isGSTBillMode ? item.GSTRate : 0.00m);
                                            cmd.Parameters.AddWithValue("@taxable", item.TaxableAmount);
                                            cmd.Parameters.AddWithValue("@cgst", isGSTBillMode ? item.CGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@sgst", isGSTBillMode ? item.SGSTAmount : 0.00m);
                                            cmd.Parameters.AddWithValue("@igst", isGSTBillMode ? item.IGSTAmount : 0.00m);
                                            cmd.ExecuteNonQuery();
                                        }

                                        using (SqlCommand cmd = new SqlCommand("UPDATE Products SET Stock = Stock - @qty WHERE Id = @prodId", conn, trans))
                                        {
                                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                                            cmd.Parameters.AddWithValue("@prodId", item.ItemId);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }

                                // 3. If this sale originated from an appointment checkout, mark the appointment as Billed now that payment is collected!
                                if (currentAppointmentId > 0)
                                {
                                    try
                                    {
                                        using (SqlCommand cmdAppt = new SqlCommand("UPDATE Appointments SET Status = 'Billed', SaleId = @saleId WHERE Id = @apptId", conn, trans))
                                        {
                                            cmdAppt.Parameters.AddWithValue("@saleId", newSaleId);
                                            cmdAppt.Parameters.AddWithValue("@apptId", currentAppointmentId);
                                            cmdAppt.ExecuteNonQuery();
                                        }
                                        using (SqlCommand cmdSalesAppt = new SqlCommand("UPDATE Sales SET AppointmentId = @apptId WHERE Id = @saleId", conn, trans))
                                        {
                                            cmdSalesAppt.Parameters.AddWithValue("@apptId", currentAppointmentId);
                                            cmdSalesAppt.Parameters.AddWithValue("@saleId", newSaleId);
                                            cmdSalesAppt.ExecuteNonQuery();
                                        }
                                    }
                                    catch { }
                                    currentAppointmentId = 0;
                                }

                                trans.Commit();
                                lastSaleId = newSaleId;

                                string billTypeLabel = isGSTBillMode ? "GST Tax Invoice" : "Non-GST Retail Bill";
                                MessageBox.Show($"{billTypeLabel} generated successfully!\nNumber: {invoiceNum}\nTotal: Rs. {grandTotal:N2}", "Sale Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                try
                                {
                                    ThermalReceiptPrinter.ShowPreview(lastSaleId);
                                }
                                catch
                                {
                                    ThermalReceiptPrinter.Print(lastSaleId);
                                }

                                // Reset cart and tender state
                                cartItems.Clear();
                                txtDiscountVal.Text = "0";
                                splitCashAmount = 0;
                                splitOnlineAmount = 0;
                                SelectPaymentMode("Cash", false);
                                UpdateCartUI();
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during checkout: {ex.Message}", "Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LoadInvoiceForAdjustment(int saleId, int apptId = 0)
        {
            if (saleId <= 0 && apptId > 0)
            {
                // Try resolving saleId by AppointmentId or Appointment table SaleId
                try
                {
                    using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SaleId, 0) FROM Appointments WHERE Id = @aId", conn))
                        {
                            cmd.Parameters.AddWithValue("@aId", apptId);
                            object obj = cmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value && Convert.ToInt32(obj) > 0)
                            {
                                saleId = Convert.ToInt32(obj);
                            }
                        }
                        if (saleId <= 0)
                        {
                            using (SqlCommand cmd2 = new SqlCommand("SELECT TOP 1 Id FROM Sales WHERE AppointmentId = @aId ORDER BY Id DESC", conn))
                            {
                                cmd2.Parameters.AddWithValue("@aId", apptId);
                                object obj = cmd2.ExecuteScalar();
                                if (obj != null && obj != DBNull.Value && Convert.ToInt32(obj) > 0)
                                {
                                    saleId = Convert.ToInt32(obj);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (saleId <= 0)
            {
                MessageBox.Show("No saved sale invoice was found for this appointment.", "Invoice Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            editingSaleId = saleId;
            currentAppointmentId = apptId;

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // 1. Fetch Sales Header
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT s.Id, s.InvoiceNumber, s.CustomerId, s.SubTotal, s.Discount, s.Tax, s.GrandTotal,
                               s.AmountPaid, s.PaymentMethod, s.IsGSTBill, s.CashAmount, s.OnlineAmount,
                               s.CustomerGSTIN, s.PlaceOfSupply, s.IsInterState,
                               c.Name AS CustomerName, c.Phone AS CustomerPhone,
                               ISNULL(c.StateName, 'Delhi') AS StateName, ISNULL(c.StateCode, '07') AS StateCode
                        FROM Sales s
                        LEFT JOIN Customers c ON s.CustomerId = c.Id
                        WHERE s.Id = @saleId", conn))
                    {
                        cmd.Parameters.AddWithValue("@saleId", saleId);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                editingInvoiceNumber = rdr["InvoiceNumber"].ToString();
                                currentCustomerId = rdr["CustomerId"] != DBNull.Value ? Convert.ToInt32(rdr["CustomerId"]) : 1;
                                currentCustomerName = rdr["CustomerName"]?.ToString() ?? "Walk-in Customer";
                                currentCustomerPhone = rdr["CustomerPhone"]?.ToString() ?? "0000000000";
                                currentCustomerGSTIN = rdr["CustomerGSTIN"]?.ToString() ?? "";
                                currentCustomerStateName = rdr["StateName"]?.ToString() ?? "Delhi";
                                currentCustomerStateCode = rdr["StateCode"]?.ToString() ?? "07";

                                lblCustomerName.Text = currentCustomerName;
                                lblCustomerPhone.Text = currentCustomerPhone;

                                bool isGst = rdr["IsGSTBill"] != DBNull.Value && Convert.ToBoolean(rdr["IsGSTBill"]);
                                SetBillMode(isGst);

                                decimal disc = rdr["Discount"] != DBNull.Value ? Convert.ToDecimal(rdr["Discount"]) : 0m;
                                txtDiscountVal.Text = disc.ToString("0.##");
                                comboDiscountType.SelectedItem = "Flat";

                                string payMethod = rdr["PaymentMethod"]?.ToString() ?? "Cash";
                                splitCashAmount = rdr["CashAmount"] != DBNull.Value ? Convert.ToDecimal(rdr["CashAmount"]) : 0m;
                                splitOnlineAmount = rdr["OnlineAmount"] != DBNull.Value ? Convert.ToDecimal(rdr["OnlineAmount"]) : 0m;
                                SelectPaymentMode(payMethod, false);
                            }
                            else
                            {
                                MessageBox.Show($"Sale record #{saleId} not found in database.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                ExitEditMode();
                                return;
                            }
                        }
                    }

                    // 2. Fetch SaleDetails
                    cartItems.Clear();
                    using (SqlCommand cmdDetails = new SqlCommand(@"
                        SELECT sd.Id, sd.ItemType, sd.ProductId, sd.ServiceId, sd.StaffId, sd.Quantity, sd.UnitPrice, sd.Total,
                               ISNULL(sd.HSNSAC, '') AS HSNSAC, ISNULL(sd.GSTRate, 18.00) AS GSTRate,
                               ISNULL(sd.PurchaseCostAtSale, 0) AS PurchaseCostAtSale,
                               p.Name AS ProductName, p.Code AS ProductCode, p.Category AS ProductCategory, ISNULL(p.PurchasePrice, 0) AS ProdCost,
                               srv.Name AS ServiceName, srv.Code AS ServiceCode, srv.Category AS ServiceCategory,
                               st.Name AS StaffName, st.Role AS StaffRole
                        FROM SaleDetails sd
                        LEFT JOIN Products p ON sd.ProductId = p.Id
                        LEFT JOIN Services srv ON sd.ServiceId = srv.Id
                        LEFT JOIN Staff st ON sd.StaffId = st.Id
                        WHERE sd.SaleId = @saleId
                        ORDER BY sd.Id ASC", conn))
                    {
                        cmdDetails.Parameters.AddWithValue("@saleId", saleId);
                        using (SqlDataReader rdr = cmdDetails.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string itemType = rdr["ItemType"].ToString();
                                int prodId = rdr["ProductId"] != DBNull.Value ? Convert.ToInt32(rdr["ProductId"]) : 0;
                                int srvId = rdr["ServiceId"] != DBNull.Value ? Convert.ToInt32(rdr["ServiceId"]) : 0;
                                int staffId = rdr["StaffId"] != DBNull.Value ? Convert.ToInt32(rdr["StaffId"]) : 0;
                                string staffName = rdr["StaffName"]?.ToString() ?? "";
                                int qty = Convert.ToInt32(rdr["Quantity"]);
                                decimal unitPrice = Convert.ToDecimal(rdr["UnitPrice"]);
                                decimal gstRate = Convert.ToDecimal(rdr["GSTRate"]);
                                string hsnSac = rdr["HSNSAC"].ToString();
                                decimal cost = Convert.ToDecimal(rdr["PurchaseCostAtSale"]);

                                if (itemType == "Service")
                                {
                                    string srvName = rdr["ServiceName"]?.ToString() ?? "Salon Service";
                                    string srvCode = rdr["ServiceCode"]?.ToString() ?? "SRV";
                                    string srvCat = rdr["ServiceCategory"]?.ToString() ?? "";
                                    string srvIcon = GetCategoryEmoji(srvCat, srvName);

                                    cartItems.Add(new CartItem {
                                        ItemType = "Service",
                                        ItemId = srvId,
                                        Code = srvCode,
                                        HSNSAC = !string.IsNullOrEmpty(hsnSac) ? hsnSac : "999721",
                                        Name = srvName,
                                        StaffId = staffId,
                                        StaffName = !string.IsNullOrEmpty(staffName) ? staffName : "Select Stylist",
                                        UnitPrice = unitPrice,
                                        Quantity = qty,
                                        GSTRate = gstRate,
                                        CostPrice = cost,
                                        IconEmoji = srvIcon
                                    });
                                }
                                else
                                {
                                    string prodName = rdr["ProductName"]?.ToString() ?? "Product";
                                    string prodCode = rdr["ProductCode"]?.ToString() ?? "PRD";

                                    cartItems.Add(new CartItem {
                                        ItemType = "Product",
                                        ItemId = prodId,
                                        Code = prodCode,
                                        HSNSAC = !string.IsNullOrEmpty(hsnSac) ? hsnSac : "3305",
                                        Name = prodName,
                                        StaffId = 0,
                                        StaffName = "",
                                        UnitPrice = unitPrice,
                                        Quantity = qty,
                                        GSTRate = gstRate,
                                        CostPrice = cost,
                                        IconEmoji = "🧴"
                                    });
                                }
                            }
                        }
                    }

                    // Update UI to Adjustment mode
                    if (editBannerPanel != null)
                    {
                        editBannerPanel.Visible = true;
                        lblEditBanner.Text = $"✏️ Adjusting Saved Invoice: {editingInvoiceNumber}" + (apptId > 0 ? $" (Appt #{apptId})" : "");
                    }
                    if (btnPayAndPrint != null)
                    {
                        btnPayAndPrint.Text = $"💾  UPDATE & SAVE INVOICE ( {editingInvoiceNumber} )";
                        btnPayAndPrint.BackColor = Color.FromArgb(245, 158, 11);
                    }
                    UpdateCartUI();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading invoice for adjustment: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitEditMode();
            }
        }

        public void ExitEditMode()
        {
            editingSaleId = 0;
            editingInvoiceNumber = "";
            currentAppointmentId = 0;
            if (editBannerPanel != null) editBannerPanel.Visible = false;
            if (btnPayAndPrint != null)
            {
                Theme.StylePrimaryButton(btnPayAndPrint);
                btnPayAndPrint.Text = "🖨️  PAY & PRINT ( Rs. 0.00 )";
            }
            cartItems.Clear();
            txtDiscountVal.Text = "0";
            splitCashAmount = 0;
            splitOnlineAmount = 0;
            SelectPaymentMode("Cash", false);
            UpdateCartUI();

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    EnsureWalkInCustomer(conn);
                }
            }
            catch { }
        }

        private int EnsureWalkInCustomer(SqlConnection conn, SqlTransaction trans = null)
        {
            try
            {
                if (currentCustomerId > 0)
                {
                    using (SqlCommand cmdCheck = new SqlCommand("SELECT COUNT(1) FROM Customers WHERE Id = @id", conn, trans))
                    {
                        cmdCheck.Parameters.AddWithValue("@id", currentCustomerId);
                        int count = Convert.ToInt32(cmdCheck.ExecuteScalar());
                        if (count > 0) return currentCustomerId;
                    }
                }

                using (SqlCommand cmdFind = new SqlCommand("SELECT TOP 1 Id, Name, ISNULL(Phone, '+977-9800000000') AS Phone, ISNULL(GSTIN, '') AS GSTIN, ISNULL(StateName, 'Delhi') AS StateName, ISNULL(StateCode, '07') AS StateCode FROM Customers WHERE Name LIKE '%Walk-in%' OR Phone LIKE '%9800000000%' ORDER BY Id ASC", conn, trans))
                {
                    using (SqlDataReader rdr = cmdFind.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            currentCustomerId = Convert.ToInt32(rdr["Id"]);
                            currentCustomerName = rdr["Name"]?.ToString() ?? "Walk-in Customer";
                            currentCustomerPhone = rdr["Phone"]?.ToString() ?? "+977-9800000000";
                            currentCustomerGSTIN = rdr["GSTIN"]?.ToString() ?? "";
                            currentCustomerStateName = rdr["StateName"]?.ToString() ?? "Delhi";
                            currentCustomerStateCode = rdr["StateCode"]?.ToString() ?? "07";
                            if (lblCustomerName != null) lblCustomerName.Text = currentCustomerName;
                            if (lblCustomerPhone != null) lblCustomerPhone.Text = currentCustomerPhone;
                            return currentCustomerId;
                        }
                    }
                }

                // If not found, insert Walk-in Customer without invalid columns
                using (SqlCommand cmdIns = new SqlCommand(@"
                    INSERT INTO Customers (Name, Phone, Email, Address, GSTIN, StateName, StateCode, CreatedAt)
                    OUTPUT INSERTED.Id
                    VALUES ('Walk-in Customer', '+977-9800000000', '', '', '', 'Delhi', '07', GETDATE())", conn, trans))
                {
                    object newId = cmdIns.ExecuteScalar();
                    if (newId != null && newId != DBNull.Value)
                    {
                        currentCustomerId = Convert.ToInt32(newId);
                        currentCustomerName = "Walk-in Customer";
                        currentCustomerPhone = "+977-9800000000";
                        currentCustomerGSTIN = "";
                        currentCustomerStateName = "Delhi";
                        currentCustomerStateCode = "07";
                        if (lblCustomerName != null) lblCustomerName.Text = currentCustomerName;
                        if (lblCustomerPhone != null) lblCustomerPhone.Text = currentCustomerPhone;
                        return currentCustomerId;
                    }
                }

                // Ultimate fallback: get any existing customer ID
                using (SqlCommand cmdAny = new SqlCommand("SELECT TOP 1 Id FROM Customers ORDER BY Id ASC", conn, trans))
                {
                    object anyId = cmdAny.ExecuteScalar();
                    if (anyId != null && anyId != DBNull.Value)
                    {
                        currentCustomerId = Convert.ToInt32(anyId);
                        return currentCustomerId;
                    }
                }
            }
            catch { }
            return currentCustomerId;
        }

        public void PreFillServiceCheckout(int customerId, int serviceId, int staffId)
        {
            PreFillServiceCheckout(0, customerId, new System.Collections.Generic.List<Tuple<int, int>> { Tuple.Create(serviceId, staffId) });
        }

        public void PreFillServiceCheckout(int customerId, System.Collections.Generic.List<int> serviceIds, int staffId)
        {
            var pairs = new System.Collections.Generic.List<Tuple<int, int>>();
            if (serviceIds != null)
            {
                foreach (int sId in serviceIds) pairs.Add(Tuple.Create(sId, staffId));
            }
            PreFillServiceCheckout(0, customerId, pairs);
        }

        public void PreFillServiceCheckout(int customerId, System.Collections.Generic.List<Tuple<int, int>> serviceStaffPairs)
        {
            PreFillServiceCheckout(0, customerId, serviceStaffPairs);
        }

        public void PreFillServiceCheckout(int apptId, int customerId, System.Collections.Generic.List<Tuple<int, int>> serviceStaffPairs)
        {
            currentAppointmentId = apptId;

            // Lookup Customer
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Id, Name, Phone, ISNULL(GSTIN, '') AS GSTIN, ISNULL(StateName, 'Delhi') AS StateName, ISNULL(StateCode, '07') AS StateCode FROM Customers WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", customerId);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                currentCustomerId = Convert.ToInt32(rdr["Id"]);
                                currentCustomerName = rdr["Name"].ToString();
                                currentCustomerPhone = rdr["Phone"].ToString();
                                currentCustomerGSTIN = rdr["GSTIN"].ToString();
                                currentCustomerStateName = rdr["StateName"].ToString();
                                currentCustomerStateCode = rdr["StateCode"].ToString();
                                lblCustomerName.Text = currentCustomerName;
                                lblCustomerPhone.Text = currentCustomerPhone;
                            }
                        }
                    }
                }
            }
            catch { }

            // Add services with specific per-item staff
            if (serviceStaffPairs != null && serviceStaffPairs.Count > 0)
            {
                foreach (var pair in serviceStaffPairs)
                {
                    int srvId = pair.Item1;
                    int stId = pair.Item2;
                    var srv = allServices.Find(s => s.Id == srvId);
                    var st = staffList.Find(s => s.Id == stId);
                    if (srv != null)
                    {
                        cartItems.Add(new CartItem {
                            ItemType = "Service",
                            ItemId = srv.Id,
                            Code = srv.Code,
                            HSNSAC = !string.IsNullOrEmpty(srv.SACCode) ? srv.SACCode : "999721",
                            Name = srv.Name,
                            StaffId = stId,
                            StaffName = st != null ? st.Name : "-",
                            UnitPrice = srv.Price,
                            Quantity = 1,
                            GSTRate = srv.GSTRate > 0 ? srv.GSTRate : defaultGSTRate,
                            CostPrice = 0,
                            IconEmoji = srv.IconEmoji
                        });
                    }
                }
                UpdateCartUI();
            }
        }

        private void BtnSelectCust_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 12 Id, Name, Phone, ISNULL(GSTIN, '') AS GSTIN, ISNULL(StateName, 'Delhi') AS StateName, ISNULL(StateCode, '07') AS StateCode FROM Customers ORDER BY CASE WHEN Name LIKE '%Walk-in%' THEN 0 ELSE 1 END, Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                int id = Convert.ToInt32(rdr["Id"]);
                                string name = rdr["Name"].ToString();
                                string phone = rdr["Phone"].ToString();
                                string gstin = rdr["GSTIN"].ToString();
                                string stName = rdr["StateName"].ToString();
                                string stCode = rdr["StateCode"].ToString();

                                string label = string.IsNullOrEmpty(gstin) ? $"{name} ({phone})" : $"{name} • GSTIN: {gstin}";
                                var item = menu.Items.Add(label);
                                item.Click += (s, ev) => {
                                    currentCustomerId = id;
                                    currentCustomerName = name;
                                    currentCustomerPhone = phone;
                                    currentCustomerGSTIN = gstin;
                                    currentCustomerStateName = stName;
                                    currentCustomerStateCode = stCode;
                                    lblCustomerName.Text = name;
                                    lblCustomerPhone.Text = string.IsNullOrEmpty(gstin) ? phone : $"{phone} | GST: {gstin}";
                                    RecalculateTotals();
                                };
                            }
                        }
                    }
                }
            }
            catch { }

            menu.Show(lblCustomerName, new Point(0, lblCustomerName.Height));
        }

        private void BtnAddCust_Click(object sender, EventArgs e)
        {
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter Client Name:", "New Quick Client", "New Client");
            if (string.IsNullOrEmpty(newName)) return;
            string newPhone = Microsoft.VisualBasic.Interaction.InputBox("Enter Client Phone Number:", "New Client Phone", "+977-98");

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        INSERT INTO Customers (Name, Phone, GSTIN, StateName, StateCode, Email, Address) 
                        OUTPUT INSERTED.Id 
                        VALUES (@name, @phone, NULL, @stName, @stCode, 'client@saloon.com', 'Local')", conn))
                    {
                        cmd.Parameters.AddWithValue("@name", newName);
                        cmd.Parameters.AddWithValue("@phone", newPhone);
                        cmd.Parameters.AddWithValue("@stName", salonStateName);
                        cmd.Parameters.AddWithValue("@stCode", salonStateCode);
                        int newId = (int)cmd.ExecuteScalar();

                        currentCustomerId = newId;
                        currentCustomerName = newName;
                        currentCustomerPhone = newPhone;
                        currentCustomerGSTIN = "";
                        currentCustomerStateName = salonStateName;
                        currentCustomerStateCode = salonStateCode;
                        lblCustomerName.Text = newName;
                        lblCustomerPhone.Text = newPhone;
                        RecalculateTotals();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding client: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddCustomItem_Click(object sender, EventArgs e)
        {
            string itemName = Microsoft.VisualBasic.Interaction.InputBox("Enter Custom Retail Product Name:", "Add Custom Product", "Custom Retail Item");
            if (string.IsNullOrEmpty(itemName)) return;
            string itemPriceStr = Microsoft.VisualBasic.Interaction.InputBox("Enter Product Rate / Price (Rs.):", "Product Price", "500");
            if (!decimal.TryParse(itemPriceStr, out decimal itemPrice)) return;

            cartItems.Add(new CartItem {
                ItemType = "Product",
                ItemId = 0,
                Code = "CUSTOM",
                Name = itemName,
                StaffId = 0,
                StaffName = "-",
                UnitPrice = itemPrice,
                Quantity = 1,
                CostPrice = 0,
                IconEmoji = "🧴"
            });

            UpdateCartUI();
        }

        private void InvoiceDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            int startX = 40;
            int startY = 35;
            int pageWidth = 720;

            Font fTitle = new Font("Segoe UI", 15, FontStyle.Bold);
            Font fSubTitle = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            Font fBold = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            Font fHeader = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
            Font fRegular = new Font("Segoe UI", 8F, FontStyle.Regular);
            Font fSmall = new Font("Segoe UI", 7.5F, FontStyle.Regular);
            Brush bDark = Brushes.Black;
            Pen pLine = new Pen(Color.FromArgb(180, 180, 180), 1);
            Pen pThick = new Pen(Color.FromArgb(100, 100, 100), 1.5F);

            string invNum = "", dateStr = "", paymentMode = "", custName = "", custPhone = "", custGst = "", pos = "";
            decimal sub = 0, disc = 0, tx = 0, grand = 0, taxable = 0, cgst = 0, sgst = 0, igst = 0, cashAmt = 0, onlineAmt = 0;
            bool isGstBill = true, isInter = false;

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT s.InvoiceNumber, s.SaleDate, s.SubTotal, s.Discount, s.Tax, s.GrandTotal, s.PaymentMethod, 
                               ISNULL(c.Name, 'Walk-in Customer') AS CustomerName, ISNULL(c.Phone, '') AS CustomerPhone,
                               ISNULL(s.IsGSTBill, 1) AS IsGSTBill, ISNULL(s.TaxableAmount, 0) AS TaxableAmount,
                               ISNULL(s.CGSTAmount, 0) AS CGSTAmount, ISNULL(s.SGSTAmount, 0) AS SGSTAmount, ISNULL(s.IGSTAmount, 0) AS IGSTAmount,
                               ISNULL(s.CustomerGSTIN, '') AS CustomerGSTIN, ISNULL(s.PlaceOfSupply, '') AS PlaceOfSupply,
                               ISNULL(s.IsInterState, 0) AS IsInterState,
                               ISNULL(s.CashAmount, 0) AS CashAmount, ISNULL(s.OnlineAmount, 0) AS OnlineAmount
                        FROM Sales s 
                        LEFT JOIN Customers c ON s.CustomerId = c.Id 
                        WHERE s.Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", lastSaleId);
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                invNum = r.GetString(0);
                                dateStr = r.GetDateTime(1).ToString("dd-MMM-yyyy HH:mm");
                                sub = r.GetDecimal(2);
                                disc = r.GetDecimal(3);
                                tx = r.GetDecimal(4);
                                grand = r.GetDecimal(5);
                                paymentMode = r.GetString(6);
                                custName = r.GetString(7);
                                custPhone = r.GetString(8);
                                isGstBill = Convert.ToBoolean(r["IsGSTBill"]);
                                taxable = Convert.ToDecimal(r["TaxableAmount"]);
                                cgst = Convert.ToDecimal(r["CGSTAmount"]);
                                sgst = Convert.ToDecimal(r["SGSTAmount"]);
                                igst = Convert.ToDecimal(r["IGSTAmount"]);
                                custGst = r["CustomerGSTIN"].ToString();
                                pos = r["PlaceOfSupply"].ToString();
                                isInter = Convert.ToBoolean(r["IsInterState"]);
                                cashAmt = Convert.ToDecimal(r["CashAmount"]);
                                onlineAmt = Convert.ToDecimal(r["OnlineAmount"]);
                            }
                        }
                    }
                }
            }
            catch { }

            // ==========================================
            // HEADER SECTION
            // ==========================================
            string billTitle = isGstBill ? "TAX INVOICE" : "RETAIL CASH RECEIPT";
            g.DrawString(salonShopName, fTitle, bDark, startX, startY);
            g.DrawString($"{salonAddress} | Tel: {salonPhone} | Email: {salonEmail}", fSubTitle, bDark, startX, startY + 26);
            if (isGstBill && !string.IsNullOrEmpty(salonGSTIN))
            {
                g.DrawString($"GSTIN: {salonGSTIN} | State: {salonStateName} (Code: {salonStateCode})", fBold, bDark, startX, startY + 44);
            }
            else if (!isGstBill)
            {
                g.DrawString($"State: {salonStateName} | Mode: Retail / Non-GST Sale", fSubTitle, bDark, startX, startY + 44);
            }

            // QR Code for payment / verification
            string qrPaymentOrInvoiceData = invNum;
            if (salonPrintQROnReceipt && !string.IsNullOrWhiteSpace(salonUPIId))
            {
                qrPaymentOrInvoiceData = BarcodeHelper.GenerateUPIString(salonUPIId, salonUPIName, grand, invNum);
            }
            BarcodeHelper.DrawQRCode(g, qrPaymentOrInvoiceData, startX + pageWidth - 65, startY - 5, 60);

            // Document Title Banner
            int bannerY = startY + 65;
            g.DrawLine(pThick, startX, bannerY, startX + pageWidth, bannerY);
            g.DrawString(billTitle, new Font("Segoe UI", 10, FontStyle.Bold), bDark, startX + (pageWidth / 2) - 45, bannerY + 4);
            g.DrawLine(pThick, startX, bannerY + 22, startX + pageWidth, bannerY + 22);

            // Invoice Meta Info
            int metaY = bannerY + 28;
            g.DrawString($"Invoice No:   {invNum}", fBold, bDark, startX, metaY);
            g.DrawString($"Date & Time:  {dateStr}", fRegular, bDark, startX + 450, metaY);

            g.DrawString($"Client Name:  {custName}  {(!string.IsNullOrEmpty(custPhone) ? "(" + custPhone + ")" : "")}", fBold, bDark, startX, metaY + 20);
            if (isGstBill)
            {
                string custGstText = !string.IsNullOrEmpty(custGst) ? custGst : "URP (Unregistered Consumer)";
                g.DrawString($"Client GSTIN: {custGstText}", fRegular, bDark, startX, metaY + 38);
                g.DrawString($"Place of Supply: {pos}", fRegular, bDark, startX + 450, metaY + 38);
                metaY += 58;
            }
            else
            {
                metaY += 42;
            }

            g.DrawLine(pLine, startX, metaY, startX + pageWidth, metaY);

            // ==========================================
            // LINE ITEMS TABLE
            // ==========================================
            int rowY = metaY + 6;

            if (isGstBill)
            {
                // GST Columns
                int cDesc = startX;
                int cHSN = startX + 220;
                int cStaff = startX + 300;
                int cQty = startX + 420;
                int cRate = startX + 465;
                int cTaxable = startX + 535;
                int cGst = startX + 615;
                int cTot = startX + 665;

                g.DrawString("Description", fHeader, bDark, cDesc, rowY);
                g.DrawString("HSN/SAC", fHeader, bDark, cHSN, rowY);
                g.DrawString("Stylist", fHeader, bDark, cStaff, rowY);
                g.DrawString("Qty", fHeader, bDark, cQty, rowY);
                g.DrawString("Rate", fHeader, bDark, cRate, rowY);
                g.DrawString("Taxable", fHeader, bDark, cTaxable, rowY);
                g.DrawString("GST%", fHeader, bDark, cGst, rowY);
                g.DrawString("Total", fHeader, bDark, cTot, rowY);

                g.DrawLine(pThick, startX, rowY + 18, startX + pageWidth, rowY + 18);
                rowY += 24;

                var hsnSummary = new Dictionary<string, (decimal Taxable, decimal GSTRate, decimal CGST, decimal SGST, decimal IGST)>();

                try
                {
                    using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT 
                                ISNULL(sd.ItemType, 'Product') AS ItemType,
                                CASE WHEN sd.ItemType = 'Service' THEN s.Name ELSE p.Name END AS ItemName,
                                ISNULL(sd.HSNSAC, '999721') AS HSNSAC,
                                ISNULL(st.Name, '-') AS StylistName,
                                sd.Quantity, sd.UnitPrice, sd.Total,
                                ISNULL(sd.GSTRate, 18.00) AS GSTRate,
                                ISNULL(sd.TaxableAmount, sd.Total) AS TaxableAmount,
                                ISNULL(sd.CGSTAmount, 0) AS CGSTAmount,
                                ISNULL(sd.SGSTAmount, 0) AS SGSTAmount,
                                ISNULL(sd.IGSTAmount, 0) AS IGSTAmount
                            FROM SaleDetails sd
                            LEFT JOIN Products p ON sd.ProductId = p.Id
                            LEFT JOIN Services s ON sd.ServiceId = s.Id
                            LEFT JOIN Staff st ON sd.StaffId = st.Id
                            WHERE sd.SaleId = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", lastSaleId);
                            using (SqlDataReader r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string itemName = r.GetString(1);
                                    string hsn = r.GetString(2);
                                    string stylist = r.GetString(3);
                                    int qty = r.GetInt32(4);
                                    decimal rate = r.GetDecimal(5);
                                    decimal total = r.GetDecimal(6);
                                    decimal gRate = r.GetDecimal(7);
                                    decimal lineTaxable = r.GetDecimal(8);
                                    decimal lineCgst = r.GetDecimal(9);
                                    decimal lineSgst = r.GetDecimal(10);
                                    decimal lineIgst = r.GetDecimal(11);

                                    // Print row
                                    g.DrawString(itemName.Length > 28 ? itemName.Substring(0, 26) + ".." : itemName, fRegular, bDark, cDesc, rowY);
                                    g.DrawString(hsn, fSmall, bDark, cHSN, rowY);
                                    g.DrawString(stylist.Length > 16 ? stylist.Substring(0, 14) + ".." : stylist, fSmall, bDark, cStaff, rowY);
                                    g.DrawString(qty.ToString(), fRegular, bDark, cQty, rowY);
                                    g.DrawString($"{rate:F0}", fRegular, bDark, cRate, rowY);
                                    g.DrawString($"{lineTaxable:F2}", fRegular, bDark, cTaxable, rowY);
                                    g.DrawString($"{gRate:0}%", fSmall, bDark, cGst, rowY);
                                    g.DrawString($"{total:F2}", fBold, bDark, cTot, rowY);
                                    rowY += 20;

                                    // Add to HSN summary
                                    string key = $"{hsn}_{gRate:0}";
                                    if (hsnSummary.ContainsKey(key))
                                    {
                                        var cur = hsnSummary[key];
                                        hsnSummary[key] = (cur.Taxable + lineTaxable, gRate, cur.CGST + lineCgst, cur.SGST + lineSgst, cur.IGST + lineIgst);
                                    }
                                    else
                                    {
                                        hsnSummary[key] = (lineTaxable, gRate, lineCgst, lineSgst, lineIgst);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                g.DrawLine(pLine, startX, rowY + 4, startX + pageWidth, rowY + 4);
                rowY += 12;

                // Totals & Calculations
                int calcX = startX + 460;
                int valX = startX + 630;

                g.DrawString("Gross SubTotal:", fRegular, bDark, calcX, rowY);
                g.DrawString($"Rs. {sub:N2}", fRegular, bDark, valX, rowY);
                rowY += 18;

                g.DrawString("Discount:", fRegular, bDark, calcX, rowY);
                g.DrawString($"- Rs. {disc:N2}", fRegular, bDark, valX, rowY);
                rowY += 18;

                g.DrawString("Taxable Value:", fBold, bDark, calcX, rowY);
                g.DrawString($"Rs. {taxable:N2}", fBold, bDark, valX, rowY);
                rowY += 18;

                if (isInter)
                {
                    g.DrawString("IGST Tax:", fRegular, bDark, calcX, rowY);
                    g.DrawString($"Rs. {igst:N2}", fRegular, bDark, valX, rowY);
                    rowY += 18;
                }
                else
                {
                    g.DrawString("CGST (Central Tax):", fRegular, bDark, calcX, rowY);
                    g.DrawString($"Rs. {cgst:N2}", fRegular, bDark, valX, rowY);
                    rowY += 18;

                    g.DrawString("SGST (State Tax):", fRegular, bDark, calcX, rowY);
                    g.DrawString($"Rs. {sgst:N2}", fRegular, bDark, valX, rowY);
                    rowY += 18;
                }

                g.DrawLine(pThick, calcX - 10, rowY, startX + pageWidth, rowY);
                rowY += 6;

                g.DrawString("TOTAL AMOUNT:", new Font("Segoe UI", 10, FontStyle.Bold), bDark, calcX, rowY);
                g.DrawString($"Rs. {grand:N2}", new Font("Segoe UI", 10, FontStyle.Bold), bDark, valX, rowY);
                rowY += 26;

                // GST HSN Tax Summary Table
                if (hsnSummary.Count > 0)
                {
                    g.DrawString("📊 GST Tax Summary Breakdown Table", fBold, bDark, startX, rowY);
                    rowY += 18;
                    g.DrawLine(pThick, startX, rowY, startX + pageWidth, rowY);
                    rowY += 4;

                    int sHsn = startX;
                    int sTaxable = startX + 100;
                    int sCgst = startX + 240;
                    int sSgst = startX + 380;
                    int sIgst = startX + 520;
                    int sTotTax = startX + 640;

                    g.DrawString("HSN/SAC", fHeader, bDark, sHsn, rowY);
                    g.DrawString("Taxable Value", fHeader, bDark, sTaxable, rowY);
                    g.DrawString("Central Tax (CGST)", fHeader, bDark, sCgst, rowY);
                    g.DrawString("State Tax (SGST)", fHeader, bDark, sSgst, rowY);
                    g.DrawString("Integrated (IGST)", fHeader, bDark, sIgst, rowY);
                    g.DrawString("Total Tax", fHeader, bDark, sTotTax, rowY);
                    rowY += 16;
                    g.DrawLine(pLine, startX, rowY, startX + pageWidth, rowY);
                    rowY += 6;

                    foreach (var kv in hsnSummary)
                    {
                        string hsnCode = kv.Key.Split('_')[0];
                        decimal gRate = kv.Value.GSTRate;
                        decimal tVal = kv.Value.Taxable;
                        decimal cAmt = kv.Value.CGST;
                        decimal sAmt = kv.Value.SGST;
                        decimal iAmt = kv.Value.IGST;
                        decimal totLineTax = cAmt + sAmt + iAmt;

                        g.DrawString(hsnCode, fSmall, bDark, sHsn, rowY);
                        g.DrawString($"Rs. {tVal:N2}", fSmall, bDark, sTaxable, rowY);
                        g.DrawString($"{(gRate / 2m):0.#}% : Rs. {cAmt:N2}", fSmall, bDark, sCgst, rowY);
                        g.DrawString($"{(gRate / 2m):0.#}% : Rs. {sAmt:N2}", fSmall, bDark, sSgst, rowY);
                        g.DrawString($"{gRate:0.#}% : Rs. {iAmt:N2}", fSmall, bDark, sIgst, rowY);
                        g.DrawString($"Rs. {totLineTax:N2}", fBold, bDark, sTotTax, rowY);
                        rowY += 18;
                    }
                    g.DrawLine(pThick, startX, rowY, startX + pageWidth, rowY);
                    rowY += 10;
                }

                // Amount in Words
                g.DrawString($"Amount in Words: {IndianGSTHelper.AmountToWords(grand)}", fBold, bDark, startX, rowY);
                rowY += 18;
            }
            else
            {
                // NON-GST BILL / RETAIL RECEIPT LAYOUT
                int cDesc = startX;
                int cStaff = startX + 320;
                int cQty = startX + 480;
                int cRate = startX + 560;
                int cTot = startX + 640;

                g.DrawString("Description", fHeader, bDark, cDesc, rowY);
                g.DrawString("Staff / Specialist", fHeader, bDark, cStaff, rowY);
                g.DrawString("Qty", fHeader, bDark, cQty, rowY);
                g.DrawString("Rate (Rs.)", fHeader, bDark, cRate, rowY);
                g.DrawString("Total (Rs.)", fHeader, bDark, cTot, rowY);

                g.DrawLine(pThick, startX, rowY + 18, startX + pageWidth, rowY + 18);
                rowY += 24;

                try
                {
                    using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT 
                                ISNULL(sd.ItemType, 'Product') AS ItemType,
                                CASE WHEN sd.ItemType = 'Service' THEN s.Name ELSE p.Name END AS ItemName,
                                ISNULL(st.Name, '-') AS StylistName,
                                sd.Quantity, sd.UnitPrice, sd.Total
                            FROM SaleDetails sd
                            LEFT JOIN Products p ON sd.ProductId = p.Id
                            LEFT JOIN Services s ON sd.ServiceId = s.Id
                            LEFT JOIN Staff st ON sd.StaffId = st.Id
                            WHERE sd.SaleId = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", lastSaleId);
                            using (SqlDataReader r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    g.DrawString(r.GetString(1), fRegular, bDark, cDesc, rowY);
                                    g.DrawString(r.GetString(2), fRegular, bDark, cStaff, rowY);
                                    g.DrawString(r.GetInt32(3).ToString(), fRegular, bDark, cQty, rowY);
                                    g.DrawString($"Rs. {r.GetDecimal(4):F2}", fRegular, bDark, cRate, rowY);
                                    g.DrawString($"Rs. {r.GetDecimal(5):F2}", fBold, bDark, cTot, rowY);
                                    rowY += 22;
                                }
                            }
                        }
                    }
                }
                catch { }

                g.DrawLine(pLine, startX, rowY + 6, startX + pageWidth, rowY + 6);
                rowY += 14;

                int calcX = startX + 460;
                int valX = startX + 630;

                g.DrawString("Sub Total:", fRegular, bDark, calcX, rowY);
                g.DrawString($"Rs. {sub:N2}", fRegular, bDark, valX, rowY);
                rowY += 20;

                g.DrawString("Discount:", fRegular, bDark, calcX, rowY);
                g.DrawString($"- Rs. {disc:N2}", fRegular, bDark, valX, rowY);
                rowY += 20;

                g.DrawLine(pThick, calcX - 10, rowY, startX + pageWidth, rowY);
                rowY += 6;

                g.DrawString("TOTAL PAYABLE:", new Font("Segoe UI", 10, FontStyle.Bold), bDark, calcX, rowY);
                g.DrawString($"Rs. {grand:N2}", new Font("Segoe UI", 10, FontStyle.Bold), bDark, valX, rowY);
                rowY += 26;

                g.DrawString($"Amount in Words: {IndianGSTHelper.AmountToWords(grand)}", fBold, bDark, startX, rowY);
                rowY += 18;
            }

            // ==========================================
            // FOOTER & SIGNATURE
            // ==========================================
            if (paymentMode == "Split" || (cashAmt > 0 && onlineAmt > 0))
            {
                g.DrawString($"Payment Received via: Split Tender (Cash: Rs. {cashAmt:N2} | Online: Rs. {onlineAmt:N2})", fBold, bDark, startX, rowY);
            }
            else
            {
                g.DrawString($"Payment Received via: {paymentMode}", fBold, bDark, startX, rowY);
            }
            rowY += 22;

            g.DrawLine(pLine, startX, rowY, startX + pageWidth, rowY);
            rowY += 15;

            g.DrawString("Terms & Conditions: Services/Goods once sold are non-refundable. Thank you for your patronage!", fSmall, bDark, startX, rowY);
            g.DrawString("Authorized Signatory", fBold, bDark, startX + pageWidth - 140, rowY + 20);
        }
    }
}
