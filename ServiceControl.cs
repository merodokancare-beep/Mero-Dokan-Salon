using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace MeroDokan
{
    public class ServiceControl : UserControl
    {
        private TextBox txtCode;
        private TextBox txtSAC;
        private TextBox txtName;
        private ComboBox comboCategory;
        private ComboBox comboGSTRate;
        private TextBox txtPrice;
        private NumericUpDown numDuration;
        private TextBox txtDescription;
        private CheckBox chkIsActive;
        private TextBox txtSearch;
        private ComboBox comboFilterCategory;
        private System.Collections.Generic.Dictionary<string, (string Sac, decimal Gst)> categoryMapping = new System.Collections.Generic.Dictionary<string, (string Sac, decimal Gst)>();
        private System.Collections.Generic.Dictionary<string, string> codeToCategoryMapping = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private DataGridView gridServices;
        private Button btnSave;
        private Button btnClear;
        private Button btnDelete;

        private int selectedServiceId = 0;

        public ServiceControl()
        {
            InitializeComponent();
            LoadCategories();
            LoadServices();
            ResetForm();
            this.Load += (s, e) => txtName.Focus();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1000, 680);
            this.AutoScroll = true;
            this.BackColor = Theme.Secondary;

            // Page Title Header
            Label lblHeader = new Label();
            lblHeader.Text = "✂️ Saloon & Spa Services Master";
            lblHeader.Location = new Point(20, 15);
            lblHeader.AutoSize = true;
            Theme.StyleLabel(lblHeader, Theme.TextLight, Theme.HeaderFont);
            this.Controls.Add(lblHeader);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "Configure haircutting, styling, facial treatments, hair spa, and grooming packages";
            lblSubtitle.Location = new Point(22, 45);
            lblSubtitle.AutoSize = true;
            Theme.StyleLabel(lblSubtitle, Theme.TextDark, Theme.MainFont);
            this.Controls.Add(lblSubtitle);

            // LEFT PANEL: Service Creation / Edit Card
            Panel entryPanel = Theme.CreateCard(340, 580);
            entryPanel.Location = new Point(20, 75);
            entryPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            Label lblCardTitle = new Label();
            lblCardTitle.Text = "Service Details";
            lblCardTitle.Location = new Point(15, 12);
            Theme.StyleLabel(lblCardTitle, Theme.TextLight, Theme.SubHeaderFont);
            entryPanel.Controls.Add(lblCardTitle);

            int startY = 40;
            int gap = 48;

            // Service Code & SAC Code side by side
            Label lblCode = new Label();
            lblCode.Text = "Code *";
            lblCode.Location = new Point(15, startY);
            lblCode.AutoSize = true;
            Theme.StyleLabel(lblCode, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblCode);

            txtCode = new TextBox();
            txtCode.Size = new Size(145, 28);
            txtCode.Location = new Point(15, startY + 18);
            Theme.StyleTextBox(txtCode);
            entryPanel.Controls.Add(txtCode);

            Label lblSAC = new Label();
            lblSAC.Text = "SAC Code *";
            lblSAC.Location = new Point(175, startY);
            lblSAC.AutoSize = true;
            Theme.StyleLabel(lblSAC, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblSAC);

            txtSAC = new TextBox();
            txtSAC.Size = new Size(110, 28);
            txtSAC.Location = new Point(175, startY + 18);
            Theme.StyleTextBox(txtSAC);
            txtSAC.Text = "999721";
            txtSAC.TextChanged += (s, e) => {
                string sac = txtSAC.Text.Trim();
                if (!string.IsNullOrEmpty(sac) && codeToCategoryMapping != null && codeToCategoryMapping.ContainsKey(sac))
                {
                    string matchedCat = codeToCategoryMapping[sac];
                    if (comboCategory.SelectedItem?.ToString() != matchedCat)
                    {
                        comboCategory.SelectedItem = matchedCat;
                    }
                }
            };
            entryPanel.Controls.Add(txtSAC);

            Button btnLookupSAC = new Button();
            btnLookupSAC.Text = "🔍";
            btnLookupSAC.Size = new Size(36, 28);
            btnLookupSAC.Location = new Point(290, startY + 18);
            Theme.StyleSecondaryButton(btnLookupSAC);
            btnLookupSAC.Click += (s, e) => {
                using (HsnSacLookupDialog dlg = new HsnSacLookupDialog("SAC"))
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        txtSAC.Text = dlg.SelectedCode;
                        if (comboGSTRate != null)
                        {
                            if (dlg.SelectedGSTRate == 0m) comboGSTRate.SelectedIndex = 0;
                            else if (dlg.SelectedGSTRate == 5m) comboGSTRate.SelectedIndex = 1;
                            else if (dlg.SelectedGSTRate == 12m) comboGSTRate.SelectedIndex = 2;
                            else if (dlg.SelectedGSTRate == 28m) comboGSTRate.SelectedIndex = 4;
                            else comboGSTRate.SelectedIndex = 3; // 18%
                        }

                        if (!string.IsNullOrEmpty(dlg.SelectedCode) && codeToCategoryMapping != null && codeToCategoryMapping.ContainsKey(dlg.SelectedCode))
                        {
                            string matchedCat = codeToCategoryMapping[dlg.SelectedCode];
                            comboCategory.SelectedItem = matchedCat;
                        }
                    }
                }
            };
            entryPanel.Controls.Add(btnLookupSAC);

            // Service Name
            Label lblName = new Label();
            lblName.Text = "Service Name *";
            lblName.Location = new Point(15, startY + gap);
            lblName.AutoSize = true;
            Theme.StyleLabel(lblName, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblName);

            txtName = new TextBox();
            txtName.Size = new Size(310, 28);
            txtName.Location = new Point(15, startY + gap + 18);
            Theme.StyleTextBox(txtName);
            entryPanel.Controls.Add(txtName);

            // Category & GST Slab
            Label lblCat = new Label();
            lblCat.Text = "Category *";
            lblCat.Location = new Point(15, startY + gap * 2);
            lblCat.AutoSize = true;
            Theme.StyleLabel(lblCat, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblCat);

            comboCategory = new ComboBox();
            comboCategory.Size = new Size(110, 28);
            comboCategory.Location = new Point(15, startY + gap * 2 + 18);
            comboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            Theme.StyleComboBox(comboCategory);
            entryPanel.Controls.Add(comboCategory);

            Button btnAddCategory = new Button();
            btnAddCategory.Text = "➕";
            btnAddCategory.Size = new Size(32, 28);
            btnAddCategory.Location = new Point(128, startY + gap * 2 + 18);
            Theme.StyleSuccessButton(btnAddCategory);
            btnAddCategory.Click += (s, e) => ShowAddCategoryDialog();
            entryPanel.Controls.Add(btnAddCategory);

            Label lblGst = new Label();
            lblGst.Text = "GST Slab *";
            lblGst.Location = new Point(175, startY + gap * 2);
            lblGst.AutoSize = true;
            Theme.StyleLabel(lblGst, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblGst);

            comboGSTRate = new ComboBox();
            comboGSTRate.Size = new Size(150, 28);
            comboGSTRate.Location = new Point(175, startY + gap * 2 + 18);
            comboGSTRate.DropDownStyle = ComboBoxStyle.DropDownList;
            comboGSTRate.Items.AddRange(new string[] { "0% (Exempt)", "5% GST", "12% GST", "18% GST (Standard)", "28% GST" });
            comboGSTRate.SelectedIndex = 3; // 18%
            Theme.StyleComboBox(comboGSTRate);
            entryPanel.Controls.Add(comboGSTRate);

            // Wire comboCategory change handler safely after comboGSTRate is created
            comboCategory.SelectedIndexChanged += (s, e) => {
                if (txtSAC == null || comboGSTRate == null) return;
                string selCat = comboCategory.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selCat) && categoryMapping != null && categoryMapping.ContainsKey(selCat))
                {
                    var mapping = categoryMapping[selCat];
                    if (!string.IsNullOrEmpty(mapping.Sac))
                    {
                        txtSAC.Text = mapping.Sac;
                    }
                    if (mapping.Gst == 0m) comboGSTRate.SelectedIndex = 0;
                    else if (mapping.Gst == 5m) comboGSTRate.SelectedIndex = 1;
                    else if (mapping.Gst == 12m) comboGSTRate.SelectedIndex = 2;
                    else if (mapping.Gst == 28m) comboGSTRate.SelectedIndex = 4;
                    else comboGSTRate.SelectedIndex = 3; // 18%
                }
            };

            // Service Price
            Label lblPrice = new Label();
            lblPrice.Text = "Service Price (Rs.) *";
            lblPrice.Location = new Point(15, startY + gap * 3);
            lblPrice.AutoSize = true;
            Theme.StyleLabel(lblPrice, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblPrice);

            txtPrice = new TextBox();
            txtPrice.Size = new Size(145, 28);
            txtPrice.Location = new Point(15, startY + gap * 3 + 18);
            Theme.StyleTextBox(txtPrice);
            entryPanel.Controls.Add(txtPrice);

            // Duration in Minutes
            Label lblDuration = new Label();
            lblDuration.Text = "Duration (Minutes)";
            lblDuration.Location = new Point(175, startY + gap * 3);
            lblDuration.AutoSize = true;
            Theme.StyleLabel(lblDuration, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblDuration);
            Theme.StyleLabel(lblDuration, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblDuration);

            numDuration = new NumericUpDown();
            numDuration.Size = new Size(150, 28);
            numDuration.Location = new Point(175, startY + gap * 3 + 18);
            numDuration.Minimum = 5;
            numDuration.Maximum = 480;
            numDuration.Value = 30;
            numDuration.Increment = 5;
            Theme.StyleNumericUpDown(numDuration);
            entryPanel.Controls.Add(numDuration);

            // Description
            Label lblDesc = new Label();
            lblDesc.Text = "Description / Package Inclusions";
            lblDesc.Location = new Point(15, startY + gap * 4);
            lblDesc.AutoSize = true;
            Theme.StyleLabel(lblDesc, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblDesc);

            txtDescription = new TextBox();
            txtDescription.Size = new Size(310, 55);
            txtDescription.Location = new Point(15, startY + gap * 4 + 18);
            txtDescription.Multiline = true;
            Theme.StyleTextBox(txtDescription);
            entryPanel.Controls.Add(txtDescription);

            // Is Active Checkbox
            chkIsActive = new CheckBox();
            chkIsActive.Text = "Active Service (Available for booking & billing)";
            chkIsActive.Location = new Point(15, startY + gap * 5 + 30);
            chkIsActive.Size = new Size(310, 24);
            chkIsActive.Checked = true;
            chkIsActive.ForeColor = Theme.TextLight;
            chkIsActive.Font = Theme.MainFont;
            entryPanel.Controls.Add(chkIsActive);

            // Action Buttons in Card
            btnSave = new Button();
            btnSave.Text = "💾 Save Service";
            btnSave.Size = new Size(150, 38);
            btnSave.Location = new Point(15, startY + gap * 6 + 15);
            Theme.StyleSuccessButton(btnSave);
            btnSave.Click += BtnSave_Click;
            entryPanel.Controls.Add(btnSave);

            btnClear = new Button();
            btnClear.Text = "🔄 Clear / New";
            btnClear.Size = new Size(150, 38);
            btnClear.Location = new Point(175, startY + gap * 6 + 15);
            Theme.StylePrimaryButton(btnClear);
            btnClear.Click += (s, e) => ResetForm();
            entryPanel.Controls.Add(btnClear);

            this.Controls.Add(entryPanel);

            // RIGHT PANEL: Search Filters and Services Grid
            Panel rightPanel = new Panel();
            rightPanel.Location = new Point(380, 75);
            rightPanel.Size = new Size(600, 580);
            rightPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            // Search Bar & Filter Card
            Panel searchCard = Theme.CreateCard(590, 60);
            searchCard.Location = new Point(0, 0);
            searchCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblSearch = new Label();
            lblSearch.Text = "🔍 Search:";
            lblSearch.Location = new Point(12, 20);
            lblSearch.AutoSize = true;
            Theme.StyleLabel(lblSearch, Theme.TextDark, Theme.BoldFont);
            searchCard.Controls.Add(lblSearch);

            txtSearch = new TextBox();
            txtSearch.Size = new Size(220, 26);
            txtSearch.Location = new Point(85, 17);
            Theme.StyleTextBox(txtSearch);
            txtSearch.TextChanged += (s, e) => LoadServices();
            searchCard.Controls.Add(txtSearch);

            Label lblCatFilter = new Label();
            lblCatFilter.Text = "Category:";
            lblCatFilter.Location = new Point(315, 20);
            lblCatFilter.AutoSize = true;
            Theme.StyleLabel(lblCatFilter, Theme.TextDark, Theme.BoldFont);
            searchCard.Controls.Add(lblCatFilter);

            comboFilterCategory = new ComboBox();
            comboFilterCategory.Size = new Size(180, 26);
            comboFilterCategory.Location = new Point(385, 17);
            comboFilterCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            Theme.StyleComboBox(comboFilterCategory);
            comboFilterCategory.SelectedIndexChanged += (s, e) => LoadServices();
            searchCard.Controls.Add(comboFilterCategory);

            rightPanel.Controls.Add(searchCard);

            // Services DataGridView
            gridServices = new DataGridView();
            gridServices.Location = new Point(0, 70);
            gridServices.Size = new Size(590, 450);
            gridServices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Theme.StyleGrid(gridServices);
            gridServices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridServices.MultiSelect = false;
            gridServices.CellClick += GridServices_CellClick;
            rightPanel.Controls.Add(gridServices);

            // Bottom action panel for grid
            Panel gridActions = new Panel();
            gridActions.Location = new Point(0, 530);
            gridActions.Size = new Size(590, 45);
            gridActions.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            btnDelete = new Button();
            btnDelete.Text = "🗑️ Delete Selected Service";
            btnDelete.Size = new Size(220, 38);
            btnDelete.Location = new Point(0, 0);
            Theme.StyleDangerButton(btnDelete);
            btnDelete.Click += BtnDelete_Click;
            gridActions.Controls.Add(btnDelete);

            Button btnRefresh = new Button();
            btnRefresh.Text = "🔄 Refresh List";
            btnRefresh.Size = new Size(150, 38);
            btnRefresh.Location = new Point(230, 0);
            Theme.StylePrimaryButton(btnRefresh);
            btnRefresh.Click += (s, e) => { LoadCategories(); LoadServices(); };
            gridActions.Controls.Add(btnRefresh);

            rightPanel.Controls.Add(gridActions);

            this.Controls.Add(rightPanel);
        }

        private void LoadCategories()
        {
            try
            {
                comboCategory.Items.Clear();
                comboFilterCategory.Items.Clear();
                comboFilterCategory.Items.Add("All Categories");
                categoryMapping.Clear();
                codeToCategoryMapping.Clear();

                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT Name, ISNULL(HsnSacCode, '999721') AS HsnSacCode, ISNULL(GSTRate, 18.00) AS GSTRate, ISNULL(Type, 'Service') AS Type 
                        FROM Categories 
                        ORDER BY CASE WHEN ISNULL(Type, 'Service') = 'Service' THEN 0 ELSE 1 END, Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string catName = rdr["Name"].ToString();
                                string sac = rdr["HsnSacCode"].ToString();
                                decimal gst = Convert.ToDecimal(rdr["GSTRate"]);
                                comboCategory.Items.Add(catName);
                                comboFilterCategory.Items.Add(catName);
                                categoryMapping[catName] = (sac, gst);
                                if (!string.IsNullOrEmpty(sac) && !codeToCategoryMapping.ContainsKey(sac))
                                {
                                    codeToCategoryMapping[sac] = catName;
                                }
                            }
                        }
                    }
                }

                if (comboCategory.Items.Count > 0) comboCategory.SelectedIndex = 0;
                if (comboFilterCategory.Items.Count > 0) comboFilterCategory.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading categories: {ex.Message}\n{ex.StackTrace}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowAddCategoryDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Add New Category";
                dlg.ClientSize = new Size(380, 180);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Theme.Primary;

                Label lblPrompt = new Label();
                lblPrompt.Text = "Category Name *";
                lblPrompt.Location = new Point(20, 20);
                lblPrompt.AutoSize = true;
                Theme.StyleLabel(lblPrompt, Theme.TextLight, Theme.BoldFont);
                dlg.Controls.Add(lblPrompt);

                TextBox txtNewCat = new TextBox();
                txtNewCat.Size = new Size(340, 30);
                txtNewCat.Location = new Point(20, 48);
                Theme.StyleTextBox(txtNewCat);
                dlg.Controls.Add(txtNewCat);

                Button btnSaveCat = new Button();
                btnSaveCat.Text = "➕ Add Category";
                btnSaveCat.Size = new Size(160, 38);
                btnSaveCat.Location = new Point(20, 95);
                Theme.StyleSuccessButton(btnSaveCat);
                btnSaveCat.Click += (sender, args) => {
                    string catName = txtNewCat.Text.Trim();
                    if (string.IsNullOrEmpty(catName))
                    {
                        MessageBox.Show("Category name cannot be empty.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM Categories WHERE Name = @name", conn))
                            {
                                checkCmd.Parameters.AddWithValue("@name", catName);
                                if ((int)checkCmd.ExecuteScalar() > 0)
                                {
                                    MessageBox.Show("This category already exists.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    return;
                                }
                            }

                            using (SqlCommand cmd = new SqlCommand("INSERT INTO Categories (Name) VALUES (@name)", conn))
                            {
                                cmd.Parameters.AddWithValue("@name", catName);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        LoadCategories();
                        comboCategory.SelectedItem = catName;
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error adding category: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                dlg.Controls.Add(btnSaveCat);

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.Size = new Size(160, 38);
                btnCancel.Location = new Point(200, 95);
                Theme.StyleSecondaryButton(btnCancel);
                btnCancel.Click += (sender, args) => dlg.Close();
                dlg.Controls.Add(btnCancel);

                dlg.AcceptButton = btnSaveCat;
                dlg.ShowDialog();
            }
        }

        private void LoadServices()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            Id,
                            Code AS [Service Code],
                            SACCode AS [SAC Code],
                            Name AS [Service Name],
                            Category,
                            GSTRate AS [GST %],
                            Price AS [Price (Rs.)],
                            DurationMinutes AS [Duration (Mins)],
                            Description,
                            CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS [Status]
                        FROM Services 
                        WHERE (Code LIKE @search OR SACCode LIKE @search OR Name LIKE @search OR Description LIKE @search)";

                    if (comboFilterCategory.SelectedIndex > 0)
                    {
                        query += " AND Category = @catFilter";
                    }

                    query += " ORDER BY Category ASC, Name ASC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        string searchVal = $"%{txtSearch?.Text.Trim() ?? ""}%";
                        cmd.Parameters.AddWithValue("@search", searchVal);

                        if (comboFilterCategory.SelectedIndex > 0)
                        {
                            cmd.Parameters.AddWithValue("@catFilter", comboFilterCategory.SelectedItem.ToString());
                        }

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            gridServices.DataSource = dt;
                        }
                    }
                }

                if (gridServices.Columns["Id"] != null)
                    gridServices.Columns["Id"].Visible = false;

                if (gridServices.Columns["Price (Rs.)"] != null)
                    gridServices.Columns["Price (Rs.)"].DefaultCellStyle.Format = "N2";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading services: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GridServices_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && gridServices.Rows[e.RowIndex].Cells["Id"].Value != null)
            {
                DataGridViewRow row = gridServices.Rows[e.RowIndex];
                selectedServiceId = Convert.ToInt32(row.Cells["Id"].Value);
                txtCode.Text = row.Cells["Service Code"].Value?.ToString() ?? "";
                txtSAC.Text = row.Cells["SAC Code"].Value?.ToString() ?? "999721";
                txtName.Text = row.Cells["Service Name"].Value?.ToString() ?? "";
                comboCategory.SelectedItem = row.Cells["Category"].Value?.ToString() ?? "";
                
                decimal gstRate = Convert.ToDecimal(row.Cells["GST %"].Value ?? 18);
                if (gstRate == 0m) comboGSTRate.SelectedIndex = 0;
                else if (gstRate == 5m) comboGSTRate.SelectedIndex = 1;
                else if (gstRate == 12m) comboGSTRate.SelectedIndex = 2;
                else if (gstRate == 28m) comboGSTRate.SelectedIndex = 4;
                else comboGSTRate.SelectedIndex = 3;

                txtPrice.Text = Convert.ToDecimal(row.Cells["Price (Rs.)"].Value ?? 0).ToString("F2");
                numDuration.Value = Convert.ToInt32(row.Cells["Duration (Mins)"].Value ?? 30);
                txtDescription.Text = row.Cells["Description"].Value?.ToString() ?? "";
                chkIsActive.Checked = (row.Cells["Status"].Value?.ToString() ?? "") == "Active";

                btnSave.Text = "✏️ Update Service";
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string code = txtCode.Text.Trim();
            string sac = txtSAC.Text.Trim();
            if (string.IsNullOrEmpty(sac)) sac = "999721";
            string name = txtName.Text.Trim();
            string category = comboCategory.SelectedItem?.ToString() ?? "";
            string desc = txtDescription.Text.Trim();
            int duration = (int)numDuration.Value;
            bool isActive = chkIsActive.Checked;

            decimal gstRate = 18.00m;
            if (comboGSTRate.SelectedIndex == 0) gstRate = 0.00m;
            else if (comboGSTRate.SelectedIndex == 1) gstRate = 5.00m;
            else if (comboGSTRate.SelectedIndex == 2) gstRate = 12.00m;
            else if (comboGSTRate.SelectedIndex == 4) gstRate = 28.00m;
            else gstRate = 18.00m;

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please provide both Service Code and Service Name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(txtPrice.Text.Trim(), out decimal price) || price < 0)
            {
                MessageBox.Show("Please provide a valid non-negative Service Price.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    if (selectedServiceId == 0)
                    {
                        // Check unique code
                        using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM Services WHERE Code = @code", conn))
                        {
                            checkCmd.Parameters.AddWithValue("@code", code);
                            int exists = (int)checkCmd.ExecuteScalar();
                            if (exists > 0)
                            {
                                MessageBox.Show("A service with this Code already exists. Please choose a unique code.", "Duplicate Code", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                        }

                        // Insert
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Services (Code, SACCode, Name, Category, GSTRate, Price, DurationMinutes, Description, IsActive)
                            VALUES (@code, @sac, @name, @cat, @gstRate, @price, @dur, @desc, @act)", conn))
                        {
                            cmd.Parameters.AddWithValue("@code", code);
                            cmd.Parameters.AddWithValue("@sac", sac);
                            cmd.Parameters.AddWithValue("@name", name);
                            cmd.Parameters.AddWithValue("@cat", category);
                            cmd.Parameters.AddWithValue("@gstRate", gstRate);
                            cmd.Parameters.AddWithValue("@price", price);
                            cmd.Parameters.AddWithValue("@dur", duration);
                            cmd.Parameters.AddWithValue("@desc", desc);
                            cmd.Parameters.AddWithValue("@act", isActive ? 1 : 0);
                            cmd.ExecuteNonQuery();
                        }
                        MessageBox.Show("Service added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        // Check unique code excluding current
                        using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM Services WHERE Code = @code AND Id != @id", conn))
                        {
                            checkCmd.Parameters.AddWithValue("@code", code);
                            checkCmd.Parameters.AddWithValue("@id", selectedServiceId);
                            int exists = (int)checkCmd.ExecuteScalar();
                            if (exists > 0)
                            {
                                MessageBox.Show("Another service with this Code already exists.", "Duplicate Code", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                        }

                        // Update
                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE Services 
                            SET Code = @code, SACCode = @sac, Name = @name, Category = @cat, GSTRate = @gstRate, Price = @price, 
                                DurationMinutes = @dur, Description = @desc, IsActive = @act
                            WHERE Id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@code", code);
                            cmd.Parameters.AddWithValue("@sac", sac);
                            cmd.Parameters.AddWithValue("@name", name);
                            cmd.Parameters.AddWithValue("@cat", category);
                            cmd.Parameters.AddWithValue("@gstRate", gstRate);
                            cmd.Parameters.AddWithValue("@price", price);
                            cmd.Parameters.AddWithValue("@dur", duration);
                            cmd.Parameters.AddWithValue("@desc", desc);
                            cmd.Parameters.AddWithValue("@act", isActive ? 1 : 0);
                            cmd.Parameters.AddWithValue("@id", selectedServiceId);
                            cmd.ExecuteNonQuery();
                        }
                        MessageBox.Show("Service updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                ResetForm();
                LoadServices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving service: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (gridServices.SelectedRows.Count == 0 || selectedServiceId == 0)
            {
                MessageBox.Show("Please select a service from the list to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show("Are you sure you want to delete this service? Past sales records will retain history.", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM Services WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", selectedServiceId);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Service deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetForm();
                LoadServices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting service: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetForm()
        {
            selectedServiceId = 0;
            txtCode.Text = GetNextServiceCode();
            txtName.Clear();
            txtPrice.Clear();
            txtDescription.Clear();
            numDuration.Value = 30;
            chkIsActive.Checked = true;
            if (comboCategory.Items.Count > 0) comboCategory.SelectedIndex = 0;
            btnSave.Text = "💾 Save Service";
            txtName.Focus();
        }

        private string GetNextServiceCode()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Code FROM Services WHERE Code IS NOT NULL AND Code <> ''", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            int maxNumber = 100;
                            string prefix = "SRV-";

                            while (rdr.Read())
                            {
                                string code = rdr["Code"].ToString().Trim();
                                if (string.IsNullOrEmpty(code)) continue;

                                if (code.StartsWith("SRV-", StringComparison.OrdinalIgnoreCase))
                                {
                                    string numPart = code.Substring(4);
                                    if (int.TryParse(numPart, out int n))
                                    {
                                        if (n > maxNumber) maxNumber = n;
                                    }
                                }
                                else if (code.StartsWith("SRV", StringComparison.OrdinalIgnoreCase))
                                {
                                    string numPart = code.Substring(3);
                                    if (int.TryParse(numPart, out int n))
                                    {
                                        if (n > maxNumber) maxNumber = n;
                                    }
                                }
                                else if (int.TryParse(code, out int n))
                                {
                                    if (n > maxNumber) maxNumber = n;
                                }
                                else
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(code, @"\d+");
                                    if (match.Success && int.TryParse(match.Value, out int extractedNum))
                                    {
                                        if (extractedNum > maxNumber) maxNumber = extractedNum;
                                    }
                                }
                            }

                            int nextNum = maxNumber + 1;
                            return $"{prefix}{nextNum}";
                        }
                    }
                }
            }
            catch
            {
                return "SRV-101";
            }
        }
    }
}
