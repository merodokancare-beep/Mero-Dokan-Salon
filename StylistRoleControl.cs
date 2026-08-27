using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace MeroDokan
{
    public class StylistRoleControl : UserControl
    {
        private TextBox txtRoleName;
        private TextBox txtDescription;
        private NumericUpDown numCommission;
        private CheckBox chkIsActive;
        private TextBox txtSearch;

        private DataGridView gridRoles;
        private Button btnSave;
        private Button btnClear;
        private Button btnDelete;

        private Label lblTotalRoles;
        private Label lblActiveRoles;
        private Label lblCardTitle;

        private int selectedRoleId = 0;

        public StylistRoleControl()
        {
            InitializeComponent();
            EnsureTableExists();
            LoadRoles();
            this.Load += (s, e) => txtRoleName?.Focus();
        }

        private static bool _tableChecked = false;
        private void EnsureTableExists()
        {
            if (_tableChecked) return;
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StylistRoles')
                        BEGIN
                            CREATE TABLE StylistRoles (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                RoleName NVARCHAR(100) NOT NULL UNIQUE,
                                Description NVARCHAR(500) NULL,
                                DefaultCommissionRate DECIMAL(5,2) NOT NULL DEFAULT 10.00,
                                IsActive BIT NOT NULL DEFAULT 1,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            );

                            INSERT INTO StylistRoles (RoleName, Description, DefaultCommissionRate, IsActive) VALUES
                            ('Senior Hair Stylist', 'Expert haircuts, styling, and hair transformations', 15.00, 1),
                            ('Hair Stylist', 'Standard haircuts, hair spa, and styling treatments', 10.00, 1),
                            ('Master Barber & Groomer', 'Beard grooming, royal shaves, and mens hair grooming', 12.00, 1),
                            ('Beautician & Skin Specialist', 'Facial therapies, skin treatments, waxing, and cleanups', 12.00, 1),
                            ('Colorist & Chemical Specialist', 'Hair coloring, highlights, smoothening, and keratin treatments', 15.00, 1),
                            ('Spa & Massage Therapist', 'Full body spas, head massage, reflexology, and relaxation therapies', 15.00, 1),
                            ('Nail Artist & Pedicurist', 'Manicure, pedicure, nail art, and nail extensions', 10.00, 1),
                            ('Junior Stylist / Apprentice', 'Entry-level styling and service support', 5.00, 1),
                            ('Salon Assistant', 'Shampooing, blow-drying, and general service assistance', 5.00, 1);
                        END", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    _tableChecked = true;
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1000, 680);
            this.AutoScroll = true;
            this.BackColor = Theme.Secondary;

            // Header
            Label lblHeader = new Label();
            lblHeader.Text = "👔 Stylist Role Master & Specializations";
            lblHeader.Location = new Point(20, 15);
            lblHeader.AutoSize = true;
            Theme.StyleLabel(lblHeader, Theme.TextLight, Theme.HeaderFont);
            this.Controls.Add(lblHeader);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "Configure salon roles, specialist designations, default commission rates, and skill profiles";
            lblSubtitle.Location = new Point(22, 45);
            lblSubtitle.AutoSize = true;
            Theme.StyleLabel(lblSubtitle, Theme.TextDark, Theme.MainFont);
            this.Controls.Add(lblSubtitle);

            // Summary Badges Card
            Panel statCard = Theme.CreateCard(340, 50);
            statCard.Location = new Point(20, 75);

            lblTotalRoles = new Label();
            lblTotalRoles.Text = "👔 Total Roles: 0";
            lblTotalRoles.Location = new Point(15, 15);
            lblTotalRoles.AutoSize = true;
            Theme.StyleLabel(lblTotalRoles, Theme.Accent, Theme.BoldFont);
            statCard.Controls.Add(lblTotalRoles);

            lblActiveRoles = new Label();
            lblActiveRoles.Text = "🟢 Active: 0";
            lblActiveRoles.Location = new Point(180, 15);
            lblActiveRoles.AutoSize = true;
            Theme.StyleLabel(lblActiveRoles, Theme.Success, Theme.BoldFont);
            statCard.Controls.Add(lblActiveRoles);

            this.Controls.Add(statCard);

            // LEFT PANEL: Form Entry Card
            Panel entryPanel = Theme.CreateCard(340, 515);
            entryPanel.Location = new Point(20, 135);
            entryPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            lblCardTitle = new Label();
            lblCardTitle.Text = "Stylist Role Details";
            lblCardTitle.Location = new Point(15, 12);
            Theme.StyleLabel(lblCardTitle, Theme.TextLight, Theme.SubHeaderFont);
            entryPanel.Controls.Add(lblCardTitle);

            int startY = 45;
            int gap = 58;

            // Role Title / Name
            Label lblName = new Label();
            lblName.Text = "Role Title / Designation *";
            lblName.Location = new Point(15, startY);
            lblName.AutoSize = true;
            Theme.StyleLabel(lblName, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblName);

            txtRoleName = new TextBox();
            txtRoleName.Size = new Size(310, 28);
            txtRoleName.Location = new Point(15, startY + 18);
            Theme.StyleTextBox(txtRoleName);
            entryPanel.Controls.Add(txtRoleName);

            // Description / Scope
            Label lblDesc = new Label();
            lblDesc.Text = "Responsibilities & Scope of Work";
            lblDesc.Location = new Point(15, startY + gap);
            lblDesc.AutoSize = true;
            Theme.StyleLabel(lblDesc, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblDesc);

            txtDescription = new TextBox();
            txtDescription.Size = new Size(310, 55);
            txtDescription.Location = new Point(15, startY + gap + 18);
            txtDescription.Multiline = true;
            Theme.StyleTextBox(txtDescription);
            entryPanel.Controls.Add(txtDescription);

            // Default Commission Rate (%)
            Label lblComm = new Label();
            lblComm.Text = "Default Commission Rate (%)";
            lblComm.Location = new Point(15, startY + gap + 82);
            lblComm.AutoSize = true;
            Theme.StyleLabel(lblComm, Theme.TextDark, Theme.BoldFont);
            entryPanel.Controls.Add(lblComm);

            numCommission = new NumericUpDown();
            numCommission.Size = new Size(310, 28);
            numCommission.Location = new Point(15, startY + gap + 100);
            numCommission.Minimum = 0;
            numCommission.Maximum = 100;
            numCommission.DecimalPlaces = 2;
            numCommission.Value = 10;
            Theme.StyleNumericUpDown(numCommission);
            entryPanel.Controls.Add(numCommission);

            // Active Status Checkbox
            chkIsActive = new CheckBox();
            chkIsActive.Text = "Role is Currently Active";
            chkIsActive.Location = new Point(15, startY + gap + 140);
            chkIsActive.Size = new Size(310, 24);
            chkIsActive.Checked = true;
            chkIsActive.ForeColor = Theme.TextLight;
            chkIsActive.Font = Theme.MainFont;
            entryPanel.Controls.Add(chkIsActive);

            // Action Buttons
            btnSave = new Button();
            btnSave.Text = "💾 Save Role";
            btnSave.Size = new Size(150, 40);
            btnSave.Location = new Point(15, startY + gap + 175);
            Theme.StyleSuccessButton(btnSave);
            btnSave.Click += BtnSave_Click;
            entryPanel.Controls.Add(btnSave);

            btnClear = new Button();
            btnClear.Text = "🔄 Clear Form";
            btnClear.Size = new Size(150, 40);
            btnClear.Location = new Point(175, startY + gap + 175);
            Theme.StyleSecondaryButton(btnClear);
            btnClear.Click += (s, e) => ResetForm();
            entryPanel.Controls.Add(btnClear);

            this.Controls.Add(entryPanel);

            // RIGHT PANEL: Grid and Search
            Panel rightPanel = new Panel();
            rightPanel.Location = new Point(380, 75);
            rightPanel.Size = new Size(600, 575);
            rightPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            // Search Bar
            Panel searchCard = Theme.CreateCard(600, 50);
            searchCard.Dock = DockStyle.Top;

            Label lblSearch = new Label();
            lblSearch.Text = "🔍 Search:";
            lblSearch.Location = new Point(12, 16);
            lblSearch.AutoSize = true;
            Theme.StyleLabel(lblSearch, Theme.TextDark, Theme.BoldFont);
            searchCard.Controls.Add(lblSearch);

            txtSearch = new TextBox();
            txtSearch.Size = new Size(300, 28);
            txtSearch.Location = new Point(90, 11);
            Theme.StyleTextBox(txtSearch);
            txtSearch.TextChanged += (s, e) => LoadRoles();
            searchCard.Controls.Add(txtSearch);

            rightPanel.Controls.Add(searchCard);

            // DataGridView
            gridRoles = new DataGridView();
            gridRoles.Location = new Point(0, 60);
            gridRoles.Size = new Size(600, 455);
            gridRoles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Theme.StyleGrid(gridRoles);
            gridRoles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridRoles.MultiSelect = false;
            gridRoles.SelectionChanged += GridRoles_SelectionChanged;
            rightPanel.Controls.Add(gridRoles);

            // Bottom Actions Panel
            Panel gridActions = new Panel();
            gridActions.Location = new Point(0, 525);
            gridActions.Size = new Size(600, 45);
            gridActions.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            btnDelete = new Button();
            btnDelete.Text = "🗑️ Delete Selected Role";
            btnDelete.Size = new Size(200, 38);
            btnDelete.Location = new Point(0, 0);
            Theme.StyleDangerButton(btnDelete);
            btnDelete.Click += BtnDelete_Click;
            gridActions.Controls.Add(btnDelete);

            Button btnRefresh = new Button();
            btnRefresh.Text = "🔄 Refresh List";
            btnRefresh.Size = new Size(140, 38);
            btnRefresh.Location = new Point(210, 0);
            Theme.StylePrimaryButton(btnRefresh);
            btnRefresh.Click += (s, e) => LoadRoles();
            gridActions.Controls.Add(btnRefresh);

            rightPanel.Controls.Add(gridActions);

            this.Controls.Add(rightPanel);
        }

        private void LoadRoles()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            r.Id,
                            r.RoleName AS [Role / Designation],
                            r.DefaultCommissionRate AS [Default Comm %],
                            ISNULL(r.Description, '') AS [Description],
                            CASE WHEN r.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS [Status],
                            COUNT(st.Id) AS [Assigned Stylists]
                        FROM StylistRoles r
                        LEFT JOIN Staff st ON st.Role = r.RoleName
                        WHERE (r.RoleName LIKE @search OR ISNULL(r.Description, '') LIKE @search)
                        GROUP BY r.Id, r.RoleName, r.DefaultCommissionRate, r.Description, r.IsActive
                        ORDER BY r.IsActive DESC, r.RoleName ASC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        string searchVal = $"%{txtSearch?.Text.Trim() ?? ""}%";
                        cmd.Parameters.AddWithValue("@search", searchVal);

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            gridRoles.DataSource = dt;

                            int total = dt.Rows.Count;
                            int active = 0;
                            foreach (DataRow r in dt.Rows)
                            {
                                if (r["Status"].ToString() == "Active") active++;
                            }
                            lblTotalRoles.Text = $"👔 Total Roles: {total}";
                            lblActiveRoles.Text = $"🟢 Active: {active}";
                        }
                    }
                }

                if (gridRoles.Columns["Id"] != null)
                    gridRoles.Columns["Id"].Visible = false;

                if (gridRoles.Columns["Default Comm %"] != null)
                    gridRoles.Columns["Default Comm %"].DefaultCellStyle.Format = "N2";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading stylist roles: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GridRoles_SelectionChanged(object sender, EventArgs e)
        {
            if (gridRoles.SelectedRows.Count > 0)
            {
                DataGridViewRow row = gridRoles.SelectedRows[0];
                if (row.Cells["Id"].Value != null && row.Cells["Id"].Value != DBNull.Value)
                {
                    selectedRoleId = Convert.ToInt32(row.Cells["Id"].Value);
                    txtRoleName.Text = row.Cells["Role / Designation"].Value?.ToString() ?? "";
                    txtDescription.Text = row.Cells["Description"].Value?.ToString() ?? "";

                    if (row.Cells["Default Comm %"].Value != null && decimal.TryParse(row.Cells["Default Comm %"].Value.ToString(), out decimal comm))
                    {
                        numCommission.Value = Math.Max(0, Math.Min(100, comm));
                    }
                    else
                    {
                        numCommission.Value = 10;
                    }

                    chkIsActive.Checked = (row.Cells["Status"].Value?.ToString() == "Active");

                    lblCardTitle.Text = $"Edit Role #{selectedRoleId}";
                    btnSave.Text = "✏️ Update Role";
                    Theme.StylePrimaryButton(btnSave);
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string roleName = txtRoleName.Text.Trim();
            string description = txtDescription.Text.Trim();
            decimal commission = numCommission.Value;
            bool isActive = chkIsActive.Checked;

            if (string.IsNullOrWhiteSpace(roleName))
            {
                MessageBox.Show("Please enter the Role / Designation title.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRoleName.Focus();
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Check for duplicate RoleName
                    using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM StylistRoles WHERE RoleName = @name AND Id <> @id", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@name", roleName);
                        checkCmd.Parameters.AddWithValue("@id", selectedRoleId);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                        if (count > 0)
                        {
                            MessageBox.Show($"A stylist role with the title '{roleName}' already exists. Please choose a different title.", "Duplicate Role", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            txtRoleName.Focus();
                            return;
                        }
                    }

                    if (selectedRoleId == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO StylistRoles (RoleName, Description, DefaultCommissionRate, IsActive)
                            VALUES (@name, @desc, @comm, @act)", conn))
                        {
                            cmd.Parameters.AddWithValue("@name", roleName);
                            cmd.Parameters.AddWithValue("@desc", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                            cmd.Parameters.AddWithValue("@comm", commission);
                            cmd.Parameters.AddWithValue("@act", isActive ? 1 : 0);
                            cmd.ExecuteNonQuery();
                        }
                        MessageBox.Show("Stylist role registered successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        // Check if old role name was updated so we can update Staff table as well
                        string oldRoleName = "";
                        using (SqlCommand getOld = new SqlCommand("SELECT RoleName FROM StylistRoles WHERE Id = @id", conn))
                        {
                            getOld.Parameters.AddWithValue("@id", selectedRoleId);
                            oldRoleName = getOld.ExecuteScalar()?.ToString() ?? "";
                        }

                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE StylistRoles 
                            SET RoleName = @name, Description = @desc, 
                                DefaultCommissionRate = @comm, IsActive = @act
                            WHERE Id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@name", roleName);
                            cmd.Parameters.AddWithValue("@desc", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                            cmd.Parameters.AddWithValue("@comm", commission);
                            cmd.Parameters.AddWithValue("@act", isActive ? 1 : 0);
                            cmd.Parameters.AddWithValue("@id", selectedRoleId);
                            cmd.ExecuteNonQuery();
                        }

                        if (!string.IsNullOrEmpty(oldRoleName) && !string.Equals(oldRoleName, roleName, StringComparison.OrdinalIgnoreCase))
                        {
                            using (SqlCommand syncStaff = new SqlCommand("UPDATE Staff SET Role = @newRole WHERE Role = @oldRole", conn))
                            {
                                syncStaff.Parameters.AddWithValue("@newRole", roleName);
                                syncStaff.Parameters.AddWithValue("@oldRole", oldRoleName);
                                syncStaff.ExecuteNonQuery();
                            }
                        }

                        MessageBox.Show("Stylist role updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                ResetForm();
                LoadRoles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving stylist role: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (gridRoles.SelectedRows.Count == 0 || selectedRoleId == 0)
            {
                MessageBox.Show("Please select a role from the list to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DataGridViewRow row = gridRoles.SelectedRows[0];
            string roleName = row.Cells["Role / Designation"]?.Value?.ToString() ?? "";
            int assignedCount = 0;
            if (row.Cells["Assigned Stylists"]?.Value != null && int.TryParse(row.Cells["Assigned Stylists"].Value.ToString(), out int c))
            {
                assignedCount = c;
            }

            if (assignedCount > 0)
            {
                MessageBox.Show($"Cannot delete the role '{roleName}' because {assignedCount} stylist(s) are currently assigned to this role.\n\nPlease reassign those stylists to another role before deleting.", "Role In Use", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to delete the role '{roleName}'?", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM StylistRoles WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", selectedRoleId);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Role deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetForm();
                LoadRoles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting role: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ResetForm()
        {
            selectedRoleId = 0;
            gridRoles.ClearSelection();
            if (txtRoleName != null) txtRoleName.Clear();
            if (txtDescription != null) txtDescription.Clear();
            if (numCommission != null) numCommission.Value = 10;
            if (chkIsActive != null) chkIsActive.Checked = true;

            if (lblCardTitle != null) lblCardTitle.Text = "Stylist Role Details";
            if (btnSave != null)
            {
                btnSave.Text = "💾 Save Role";
                Theme.StyleSuccessButton(btnSave);
            }
            txtRoleName?.Focus();
        }
    }
}
