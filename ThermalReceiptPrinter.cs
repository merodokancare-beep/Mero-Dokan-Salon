using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace MeroDokan
{
    /* ================= TVS RP 3220 STAR AU / 80MM THERMAL PRINTER COMPATIBILITY =================
       Target printer : TVS Electronics RP 3220 STAR AU (80mm / 3-inch direct thermal receipt printer)
       Paper roll     : Standard 80mm thermal roll (Printable width: 72mm / 2.84 inches)
       Width          : 284 GDI units (1/100 inch) = 72mm printable width
       Length         : Dynamic - auto-computed based on items, tax summary, split payment & QR code.

       Public API:
           ThermalReceiptPrinter.ShowPreview(int saleId)  -> preview sized to 80mm thermal roll
           ThermalReceiptPrinter.Print(int saleId)        -> prints directly to TVS RP 3220 or default printer
       ============================================================================================= */
    internal static class ThermalReceiptPrinter
    {
        // ---- Page geometry (GDI PrintDocument units = 1/100 of an inch) ----
        // 80mm thermal roll = 3.15 inches roll width, 72mm (2.84 inches) printable width = 284 units
        private const int PaperWidth = 284;
        private const int MarginLeft = 6;
        private const int MarginRight = 6;
        private const int UsableWidth = PaperWidth - MarginLeft - MarginRight; // 272 units

        private const string PaperName = "Thermal80mm";

        // ---- Shared layout constants (EstimateHeight stays in sync with DrawReceipt) ----
        private const float TopPad = 10;
        private const float DashGap = 5;
        private const float BillTypeH = 16;
        private const float RowMeta = 13;
        private const float ItemHeaderH = 14;
        private const float SubRowH = 10;
        private const float RowTot = 13;
        private const float ThickGap = 5;
        private const float TotalRowH = 22;
        private const float HsnHeadH = 22;
        private const float WordsH = 24;
        private const float QrSize = 56;
        private const float QrLabelH = 10;
        private const float TermsH = 20;
        private const float ThankYouH = 18;
        private const float BottomFeed = 35;
        private const int MinPageHeight = 450;

        public static void ShowPreview(int saleId)
        {
            PrintDocument doc = BuildDocument(saleId);

            PrintPreviewDialog dlg = new PrintPreviewDialog();
            dlg.Document = doc;
            dlg.Size = new Size(330, 680);
            try { ((Form)dlg).Text = "TVS RP 3220 STAR - Receipt Preview (80mm)"; } catch { }
            dlg.ShowDialog();
        }

        public static void Print(int saleId)
        {
            try
            {
                PrintDocument doc = BuildDocument(saleId);
                doc.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing to thermal printer: {ex.Message}\nPlease ensure the TVS RP 3220 STAR printer driver is installed and connected.", "Printer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static PrintDocument BuildDocument(int saleId)
        {
            ReceiptData d = LoadReceiptData(saleId);
            int pageHeight = EstimateHeight(d);

            PrintDocument doc = new PrintDocument();
            doc.DocumentName = "TVS_Receipt_" + d.InvNum;

            // Auto-detect TVS RP 3220 or installed thermal receipt printer
            string matchedPrinter = FindTvsOrThermalPrinter();
            if (!string.IsNullOrEmpty(matchedPrinter))
            {
                doc.PrinterSettings.PrinterName = matchedPrinter;
            }

            PaperSize ps = new PaperSize(PaperName, PaperWidth, pageHeight);
            ps.RawKind = (int)PaperKind.Custom;

            doc.DefaultPageSettings.PaperSize = ps;
            doc.DefaultPageSettings.Margins = new Margins(MarginLeft, MarginRight, 6, 6);
            doc.PrinterSettings.DefaultPageSettings.PaperSize = ps;
            doc.PrinterSettings.DefaultPageSettings.Margins = new Margins(MarginLeft, MarginRight, 6, 6);

            doc.PrintPage += delegate(object s, PrintPageEventArgs e)
            {
                DrawReceipt(e.Graphics, d);
                e.HasMorePages = false;   // single continuous roll page
            };

            return doc;
        }

        private static string FindTvsOrThermalPrinter()
        {
            try
            {
                // 1. Look for TVS Electronics / RP 3220 printer
                foreach (string p in PrinterSettings.InstalledPrinters)
                {
                    string lower = p.ToLowerInvariant();
                    if (lower.Contains("3220") || lower.Contains("rp 3220") || lower.Contains("rp3220") || lower.Contains("tvs"))
                    {
                        return p;
                    }
                }

                // 2. Look for POS-80 / Thermal / Receipt printer
                foreach (string p in PrinterSettings.InstalledPrinters)
                {
                    string lower = p.ToLowerInvariant();
                    if (lower.Contains("pos-80") || lower.Contains("pos80") || lower.Contains("receipt") || lower.Contains("80mm") || lower.Contains("thermal"))
                    {
                        return p;
                    }
                }
            }
            catch { }

            return null; // Fallback to Windows default printer
        }

        // =====================================================================
        //  DATA CLASSES
        // =====================================================================
        private class ThermalItem
        {
            public string Name;
            public string Stylist;
            public int Qty;
            public decimal Rate;
            public decimal Total;
            public string Hsn;
            public decimal GstRate;
            public decimal Taxable;
            public decimal Cgst;
            public decimal Sgst;
            public decimal Igst;
        }

        private class HsnGroup
        {
            public decimal GSTRate;
            public decimal Taxable;
            public decimal TaxAmount;
        }

        private class ReceiptData
        {
            public string InvNum = "";
            public string DateStr = "";
            public string PaymentMode = "";
            public decimal CashAmount = 0;
            public decimal OnlineAmount = 0;
            public string CustName = "";
            public string CustPhone = "";
            public bool IsGstBill = true;
            public bool IsInter = false;
            public decimal Sub, Disc, Grand, Taxable, Cgst, Sgst, Igst;

            public string ShopName = "";
            public string Address = "";
            public string Phone = "";
            public string Email = "";
            public string GSTIN = "";
            public string StateName = "";
            public string StateCode = "";
            public string UPIId = "";
            public string UPIName = "";
            public bool PrintQR = false;
            public string QrPayload = "";

            public List<ThermalItem> Items = new List<ThermalItem>();
            public Dictionary<string, HsnGroup> HsnGroups = new Dictionary<string, HsnGroup>(StringComparer.OrdinalIgnoreCase);
        }

        // =====================================================================
        //  DATA LOADING (mirrors SalesBillingControl's queries)
        // =====================================================================
        private static ReceiptData LoadReceiptData(int saleId)
        {
            ReceiptData d = new ReceiptData();

            using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                // ---- Salon profile ----
                try
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 ShopName, Address, Phone, Email, GSTIN, StateName, StateCode, UPIId, UPIName, PrintQROnReceipt FROM AppProfile", conn))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            d.ShopName = r["ShopName"] != DBNull.Value ? r["ShopName"].ToString() : "";
                            d.Address = r["Address"] != DBNull.Value ? r["Address"].ToString() : "";
                            d.Phone = r["Phone"] != DBNull.Value ? r["Phone"].ToString() : "";
                            d.Email = r["Email"] != DBNull.Value ? r["Email"].ToString() : "";
                            d.GSTIN = r["GSTIN"] != DBNull.Value ? r["GSTIN"].ToString() : "";
                            d.StateName = r["StateName"] != DBNull.Value ? r["StateName"].ToString() : "";
                            d.StateCode = r["StateCode"] != DBNull.Value ? r["StateCode"].ToString() : "";
                            d.UPIId = r["UPIId"] != DBNull.Value ? r["UPIId"].ToString() : "";
                            d.UPIName = r["UPIName"] != DBNull.Value ? r["UPIName"].ToString() : d.ShopName;
                            d.PrintQR = r["PrintQROnReceipt"] != DBNull.Value && Convert.ToBoolean(r["PrintQROnReceipt"]);
                        }
                    }
                }
                catch { }

                // ---- Sale header ----
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT s.InvoiceNumber, s.SaleDate, s.SubTotal, s.Discount, s.GrandTotal, s.PaymentMethod,
                           ISNULL(s.CashAmount, 0) AS CashAmount, ISNULL(s.OnlineAmount, 0) AS OnlineAmount,
                           ISNULL(c.Name, 'Walk-in Customer') AS CustomerName, ISNULL(c.Phone, '') AS CustomerPhone,
                           ISNULL(s.IsGSTBill, 1) AS IsGSTBill, ISNULL(s.TaxableAmount, 0) AS TaxableAmount,
                           ISNULL(s.CGSTAmount, 0) AS CGSTAmount, ISNULL(s.SGSTAmount, 0) AS SGSTAmount,
                           ISNULL(s.IGSTAmount, 0) AS IGSTAmount, ISNULL(s.IsInterState, 0) AS IsInterState
                    FROM Sales s
                    LEFT JOIN Customers c ON s.CustomerId = c.Id
                    WHERE s.Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", saleId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            throw new Exception("Sale #" + saleId + " was not found.");
                        }

                        d.InvNum = r.GetString(0);
                        d.DateStr = r.GetDateTime(1).ToString("dd-MMM-yyyy HH:mm");
                        d.Sub = r.GetDecimal(2);
                        d.Disc = r.GetDecimal(3);
                        d.Grand = r.GetDecimal(4);
                        d.PaymentMode = r.GetString(5);
                        d.CashAmount = r.GetDecimal(6);
                        d.OnlineAmount = r.GetDecimal(7);
                        d.CustName = r.GetString(8);
                        d.CustPhone = r.GetString(9);
                        d.IsGstBill = Convert.ToBoolean(r["IsGSTBill"]);
                        d.Taxable = Convert.ToDecimal(r["TaxableAmount"]);
                        d.Cgst = Convert.ToDecimal(r["CGSTAmount"]);
                        d.Sgst = Convert.ToDecimal(r["SGSTAmount"]);
                        d.Igst = Convert.ToDecimal(r["IGSTAmount"]);
                        d.IsInter = Convert.ToBoolean(r["IsInterState"]);
                    }
                }

                // ---- Line items + HSN groups ----
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        CASE WHEN sd.ItemType = 'Service' THEN s.Name ELSE p.Name END AS ItemName,
                        ISNULL(st.Name, '-') AS StylistName,
                        sd.Quantity, sd.UnitPrice, sd.Total,
                        ISNULL(sd.HSNSAC, '999721') AS HSNSAC,
                        ISNULL(sd.GSTRate, 18.00) AS GSTRate,
                        ISNULL(sd.TaxableAmount, sd.Total) AS TaxableAmount,
                        ISNULL(sd.CGSTAmount, 0) AS CGSTAmount,
                        ISNULL(sd.SGSTAmount, 0) AS SGSTAmount,
                        ISNULL(sd.IGSTAmount, 0) AS IGSTAmount
                    FROM SaleDetails sd
                    LEFT JOIN Products p ON sd.ProductId = p.Id
                    LEFT JOIN Services s ON sd.ServiceId = s.Id
                    LEFT JOIN Staff st ON sd.StaffId = st.Id
                    WHERE sd.SaleId = @id ORDER BY sd.Id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", saleId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            ThermalItem it = new ThermalItem();
                            it.Name = r.GetString(0);
                            it.Stylist = r.GetString(1);
                            it.Qty = r.GetInt32(2);
                            it.Rate = r.GetDecimal(3);
                            it.Total = r.GetDecimal(4);
                            it.Hsn = r.GetString(5);
                            it.GstRate = r.GetDecimal(6);
                            it.Taxable = r.GetDecimal(7);
                            it.Cgst = r.GetDecimal(8);
                            it.Sgst = r.GetDecimal(9);
                            it.Igst = r.GetDecimal(10);
                            d.Items.Add(it);

                            if (d.IsGstBill)
                            {
                                string key = it.Hsn + "_" + it.GstRate.ToString("0.#");
                                HsnGroup grp;
                                if (!d.HsnGroups.TryGetValue(key, out grp))
                                {
                                    grp = new HsnGroup();
                                    grp.GSTRate = it.GstRate;
                                    grp.Taxable = 0;
                                    grp.TaxAmount = 0;
                                    d.HsnGroups[key] = grp;
                                }
                                grp.Taxable += it.Taxable;
                                grp.TaxAmount += it.Cgst + it.Sgst + it.Igst;
                            }
                        }
                    }
                }
            }

            // ---- QR payload (same rule as the A4 invoice) ----
            if (d.PrintQR && !string.IsNullOrWhiteSpace(d.UPIId))
            {
                try { d.QrPayload = BarcodeHelper.GenerateUPIString(d.UPIId, d.UPIName, d.Grand, d.InvNum); }
                catch { d.QrPayload = d.InvNum; }
            }
            else
            {
                d.QrPayload = d.InvNum;
            }

            return d;
        }

        // =====================================================================
        //  HEIGHT ESTIMATION
        // =====================================================================
        private static Font FTitle() { return new Font("Segoe UI", 11F, FontStyle.Bold); }
        private static Font FSub() { return new Font("Segoe UI", 7.5F, FontStyle.Bold); }
        private static Font FBold() { return new Font("Segoe UI", 7F, FontStyle.Bold); }
        private static Font FReg() { return new Font("Segoe UI", 7F, FontStyle.Regular); }
        private static Font FSmall() { return new Font("Segoe UI", 6.25F, FontStyle.Regular); }
        private static Font FBig() { return new Font("Segoe UI", 9.5F, FontStyle.Bold); }

        private static int EstimateHeight(ReceiptData d)
        {
            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font fTitle = FTitle())
            using (Font fSub = FSub())
            using (Font fBold = FBold())
            using (Font fReg = FReg())
            {
                float y = TopPad;

                y += MeasureBlock(g, d.ShopName, fTitle) + 2;
                if (!string.IsNullOrEmpty(d.Address)) y += MeasureBlock(g, d.Address, fSub) + 1;

                string contactLine = "Tel: " + d.Phone;
                if (!string.IsNullOrEmpty(d.Email)) contactLine += " | " + d.Email;
                y += MeasureBlock(g, contactLine, fSub) + 2;

                if (d.IsGstBill && !string.IsNullOrEmpty(d.GSTIN))
                {
                    y += MeasureBlock(g, "GSTIN: " + d.GSTIN + " | " + d.StateName + " (" + d.StateCode + ")", fBold) + 2;
                }

                y += DashGap + BillTypeH + DashGap;
                y += RowMeta * 2;                                  // invoice no + date/time
                y += MeasureBlock(g, "Client: " + d.CustName + " (" + d.CustPhone + ")", fBold);
                if (d.IsGstBill) y += RowMeta;                     // state line

                y += DashGap + ItemHeaderH + DashGap;

                foreach (ThermalItem it in d.Items)
                {
                    string nameLine = it.Name + (it.Qty > 1 ? "  x" + it.Qty : "");
                    y += MeasureBlock(g, nameLine, fReg);
                    bool hasStylist = !string.IsNullOrEmpty(it.Stylist) && !it.Stylist.Equals("-");
                    if (hasStylist || it.Qty > 1) y += SubRowH;
                    y += 2;
                }

                y += DashGap + DashGap;

                y += RowTot;                                       // sub total
                if (d.Disc > 0) y += RowTot;                       // discount
                if (d.IsGstBill)
                {
                    y += RowTot;                                   // taxable value
                    if (d.IsInter) y += RowTot;                    // IGST
                    else y += RowTot * 2;                          // CGST + SGST
                }
                y += ThickGap + TotalRowH;

                if (d.IsGstBill && d.HsnGroups.Count > 0)
                {
                    y += DashGap + HsnHeadH + (d.HsnGroups.Count * RowTot) + DashGap;
                }

                y += WordsH;                                       // amount in words
                if (d.PaymentMode == "Split" || (d.CashAmount > 0 && d.OnlineAmount > 0))
                {
                    y += RowMeta * 3;                              // split payment mode + cash + online lines
                }
                else
                {
                    y += RowMeta;                                  // standard payment mode
                }

                if (d.PrintQR) y += QrSize + QrLabelH;

                y += TermsH + ThankYouH + BottomFeed;

                return Math.Max((int)Math.Ceiling(y), MinPageHeight);
            }
        }

        private static float MeasureBlock(Graphics g, string text, Font f)
        {
            SizeF sz = g.MeasureString(text ?? "", f, UsableWidth - 4);
            return sz.Height;
        }

        // =====================================================================
        //  DRAWING
        // =====================================================================
        private static void DrawReceipt(Graphics g, ReceiptData d)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Brush br = Brushes.Black;
            StringFormat ctr = new StringFormat();
            ctr.Alignment = StringAlignment.Center;
            StringFormat rightFmt = new StringFormat();
            rightFmt.Alignment = StringAlignment.Far;
            rightFmt.FormatFlags = StringFormatFlags.NoWrap;

            using (Font fTitle = FTitle())
            using (Font fSub = FSub())
            using (Font fBold = FBold())
            using (Font fReg = FReg())
            using (Font fSmall = FSmall())
            using (Font fBig = FBig())
            {
                Pen pThin = Pens.Gray;
                Pen pDash = new Pen(Color.Black, 1);
                pDash.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

                float rightEdge = MarginLeft + UsableWidth;
                float labelX = MarginLeft;
                float valueW = 78;
                float valueX = rightEdge - valueW;

                float y = TopPad;

                // ---------- HEADER ----------
                g.DrawString(d.ShopName, fTitle, br, new RectangleF(MarginLeft, y, UsableWidth, 50), ctr);
                y += MeasureBlock(g, d.ShopName, fTitle) + 2;
                if (!string.IsNullOrEmpty(d.Address))
                {
                    g.DrawString(d.Address, fSub, br, new RectangleF(MarginLeft, y, UsableWidth, 50), ctr);
                    y += MeasureBlock(g, d.Address, fSub) + 1;
                }
                string contactLine = "Tel: " + d.Phone;
                if (!string.IsNullOrEmpty(d.Email)) contactLine += " | " + d.Email;
                g.DrawString(contactLine, fSub, br, new RectangleF(MarginLeft, y, UsableWidth, 50), ctr);
                y += MeasureBlock(g, contactLine, fSub) + 2;
                if (d.IsGstBill && !string.IsNullOrEmpty(d.GSTIN))
                {
                    string gstLine = "GSTIN: " + d.GSTIN + " | " + d.StateName + " (" + d.StateCode + ")";
                    g.DrawString(gstLine, fBold, br, new RectangleF(MarginLeft, y, UsableWidth, 50), ctr);
                    y += MeasureBlock(g, gstLine, fBold) + 2;
                }

                g.DrawLine(pDash, MarginLeft, y, rightEdge, y); y += DashGap;

                // ---------- BILL TYPE ----------
                string billType = d.IsGstBill ? "TAX INVOICE" : "RETAIL CASH RECEIPT";
                g.DrawString(billType, fBold, br, new RectangleF(MarginLeft, y, UsableWidth, BillTypeH), ctr);
                y += BillTypeH;
                g.DrawLine(pDash, MarginLeft, y, rightEdge, y); y += DashGap;

                // ---------- META ----------
                g.DrawString("No: " + d.InvNum, fBold, br, labelX, y); y += RowMeta;
                g.DrawString("Date: " + d.DateStr, fReg, br, labelX, y); y += RowMeta;
                string client = "Client: " + d.CustName;
                if (!string.IsNullOrEmpty(d.CustPhone)) client += " (" + d.CustPhone + ")";
                g.DrawString(client, fBold, br, new RectangleF(labelX, y, UsableWidth - valueW + 8, 40));
                y += MeasureBlock(g, client, fBold);
                if (d.IsGstBill)
                {
                    g.DrawString("State: " + d.StateName + " (" + d.StateCode + ")", fReg, br, labelX, y);
                    y += RowMeta;
                }

                g.DrawLine(pThin, MarginLeft, y, rightEdge, y); y += DashGap;

                // ---------- ITEMS ----------
                g.DrawString("Item Description", fBold, br, labelX, y);
                g.DrawString("Amt", fBold, br, new RectangleF(valueX, y, valueW, 14), rightFmt);
                y += ItemHeaderH;
                g.DrawLine(pThin, MarginLeft, y, rightEdge, y); y += DashGap;

                foreach (ThermalItem it in d.Items)
                {
                    string nameLine = it.Name + (it.Qty > 1 ? "  x" + it.Qty : "");
                    g.DrawString(nameLine, fReg, br, new RectangleF(labelX, y, UsableWidth - valueW, 40));
                    g.DrawString("Rs." + it.Total.ToString("N2"), fBold, br, new RectangleF(valueX, y, valueW, 40), rightFmt);
                    y += MeasureBlock(g, nameLine, fReg);

                    bool hasStylist = !string.IsNullOrEmpty(it.Stylist) && !it.Stylist.Equals("-");
                    string subLine = null;
                    if (hasStylist) subLine = it.Qty + " pc @ Rs." + it.Rate.ToString("N0") + "  [" + it.Stylist + "]";
                    else if (it.Qty > 1) subLine = it.Qty + " pc @ Rs." + it.Rate.ToString("N0");

                    if (subLine != null)
                    {
                        g.DrawString(subLine, fSmall, br, labelX, y);
                        y += SubRowH;
                    }
                    y += 2;
                }

                y += DashGap;
                g.DrawLine(pDash, MarginLeft, y, rightEdge, y); y += DashGap;

                // ---------- TOTALS ----------
                Action<string, string, Font> totRow = delegate(string label, string val, Font f)
                {
                    g.DrawString(label, f, br, labelX, y);
                    g.DrawString(val, f, br, new RectangleF(valueX, y, valueW, 20), rightFmt);
                    y += RowTot;
                };

                totRow("Sub Total", "Rs. " + d.Sub.ToString("N2"), fReg);
                if (d.Disc > 0) totRow("Discount", "- Rs. " + d.Disc.ToString("N2"), fReg);
                if (d.IsGstBill)
                {
                    totRow("Taxable Value", "Rs. " + d.Taxable.ToString("N2"), fBold);
                    if (d.IsInter) totRow("IGST", "Rs. " + d.Igst.ToString("N2"), fReg);
                    else
                    {
                        totRow("CGST", "Rs. " + d.Cgst.ToString("N2"), fReg);
                        totRow("SGST", "Rs. " + d.Sgst.ToString("N2"), fReg);
                    }
                }

                g.DrawLine(pDash, labelX, y, rightEdge, y);
                y += ThickGap;

                g.DrawString("TOTAL PAYABLE", fBig, br, labelX, y);
                g.DrawString("Rs. " + d.Grand.ToString("N2"), fBig, br, new RectangleF(valueX, y, valueW, 24), rightFmt);
                y += TotalRowH;

                // ---------- HSN SUMMARY (GST bills) ----------
                if (d.IsGstBill && d.HsnGroups.Count > 0)
                {
                    g.DrawLine(pThin, MarginLeft, y, rightEdge, y); y += DashGap;
                    g.DrawString("HSN/SAC Tax Summary", fBold, br, labelX, y);
                    y += 14;
                    foreach (KeyValuePair<string, HsnGroup> kv in d.HsnGroups)
                    {
                        string hsnCode = kv.Key.Split('_')[0];
                        string lineTxt = hsnCode + " @" + kv.Value.GSTRate.ToString("0.#") + "% : Rs. " +
                                         kv.Value.Taxable.ToString("N2") + " / Tax Rs. " + kv.Value.TaxAmount.ToString("N2");
                        g.DrawString(lineTxt, fSmall, br, new RectangleF(labelX, y, UsableWidth, 30));
                        y += RowTot;
                    }
                    g.DrawLine(pDash, MarginLeft, y, rightEdge, y); y += DashGap;
                }

                // ---------- WORDS + PAYMENT ----------
                string words = "In Words: " + IndianGSTHelper.AmountToWords(d.Grand);
                g.DrawString(words, fSmall, br, new RectangleF(labelX, y, UsableWidth, 40));
                y += WordsH;

                if (d.PaymentMode == "Split" || (d.CashAmount > 0 && d.OnlineAmount > 0))
                {
                    g.DrawString("Paid via: Split Tender", fBold, br, labelX, y);
                    y += RowMeta;
                    g.DrawString("  - Cash:   Rs. " + d.CashAmount.ToString("N2"), fReg, br, labelX, y);
                    y += RowMeta;
                    g.DrawString("  - Online: Rs. " + d.OnlineAmount.ToString("N2"), fReg, br, labelX, y);
                    y += RowMeta;
                }
                else
                {
                    g.DrawString("Paid via: " + d.PaymentMode, fBold, br, labelX, y);
                    y += RowMeta;
                }

                // ---------- QR ----------
                if (d.PrintQR)
                {
                    try
                    {
                        BarcodeHelper.DrawQRCode(g, d.QrPayload, (PaperWidth - QrSize) / 2f, y, QrSize);
                    }
                    catch { }
                    y += QrSize + QrLabelH;
                }

                // ---------- FOOTER ----------
                g.DrawString("Goods/Services once sold are non-refundable.", fSmall, br, new RectangleF(MarginLeft, y, UsableWidth, 30), ctr);
                y += TermsH;
                g.DrawString("-- Thank You! Please Visit Again --", fBold, br, new RectangleF(MarginLeft, y, UsableWidth, 30), ctr);
                y += ThankYouH;
            }
        }
    }
}
