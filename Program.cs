using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace PasswordGenerator
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
        private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string NumberChars = "0123456789";
        private const string SymbolChars = "!@#$%^&*()-_=+[]{}|;:,.<>?/";

        private readonly TextBox _txtPassword;
        private readonly Button _btnCopy;
        private readonly Button _btnRefresh;
        private readonly NumericUpDown _numLength;
        private readonly TrackBar _trackLength;
        private readonly CheckBox _chkLower;
        private readonly CheckBox _chkUpper;
        private readonly CheckBox _chkNumbers;
        private readonly CheckBox _chkSymbols;
        private readonly Label _lblStatus;
        private readonly Timer _feedbackTimer;
        private readonly RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        private bool _isSyncingLength;
        private bool _isValidPassword;

        public MainForm()
        {
            Text = "Password Generator";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point);
            ClientSize = new Size(760, 480);
            Padding = new Padding(28);

            // Automatically use the icon embedded into the .exe for the window title bar
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                // Fallback to default icon if extraction fails
            }

            _feedbackTimer = new Timer { Interval = 1800 };
            _feedbackTimer.Tick += delegate
            {
                _feedbackTimer.Stop();
                ResetCopyFeedback();
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 1. Password Display & Action Buttons Row
            var passwordPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            passwordPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            passwordPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            passwordPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _txtPassword = new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 18F, FontStyle.Regular, GraphicsUnit.Point),
                BackColor = SystemColors.Window,
                Margin = new Padding(0, 4, 12, 4)
            };

            _btnRefresh = new Button
            {
                Text = "Refresh",
                Size = new Size(125, 48),
                Margin = new Padding(0, 0, 12, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = true
            };
            _btnRefresh.Click += delegate { GeneratePassword(); };

            _btnCopy = new Button
            {
                Text = "Copy",
                Size = new Size(125, 48),
                Margin = new Padding(0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = true
            };
            _btnCopy.Click += delegate { CopyPassword(); };

            var toolTip = new ToolTip();
            toolTip.SetToolTip(_btnRefresh, "Generate a new password");
            toolTip.SetToolTip(_btnCopy, "Copy password to clipboard");

            passwordPanel.Controls.Add(_txtPassword, 0, 0);
            passwordPanel.Controls.Add(_btnRefresh, 1, 0);
            passwordPanel.Controls.Add(_btnCopy, 2, 0);

            // 2. Status / Validation Message Row
            _lblStatus = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 36,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.ControlDarkDark,
                Margin = new Padding(0, 0, 0, 16)
            };

            // 3. Length Selection Row (TrackBar: 4-32, Manual Input: 4-512)
            var lengthGroup = new GroupBox
            {
                Text = "Password Length",
                Dock = DockStyle.Top,
                Height = 115,
                Padding = new Padding(18, 12, 18, 12),
                Margin = new Padding(0, 0, 0, 20)
            };

            var lengthLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            lengthLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            lengthLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _trackLength = new TrackBar
            {
                Minimum = 4,
                Maximum = 32,
                Value = 16,
                TickStyle = TickStyle.None,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 16, 0)
            };

            _numLength = new NumericUpDown
            {
                Minimum = 4,
                Maximum = 512,
                Value = 16,
                Width = 115,
                Font = new Font("Segoe UI", 15F, FontStyle.Regular, GraphicsUnit.Point),
                TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(0, 6, 0, 0)
            };

            _trackLength.ValueChanged += delegate
            {
                if (_isSyncingLength) return;
                _isSyncingLength = true;
                _numLength.Value = _trackLength.Value;
                _isSyncingLength = false;
                GeneratePassword();
            };

            _numLength.ValueChanged += delegate
            {
                if (_isSyncingLength) return;
                _isSyncingLength = true;
                int val = (int)_numLength.Value;
                if (val < _trackLength.Minimum)
                    _trackLength.Value = _trackLength.Minimum;
                else if (val > _trackLength.Maximum)
                    _trackLength.Value = _trackLength.Maximum;
                else
                    _trackLength.Value = val;
                _isSyncingLength = false;
                GeneratePassword();
            };

            lengthLayout.Controls.Add(_trackLength, 0, 0);
            lengthLayout.Controls.Add(_numLength, 1, 0);
            lengthGroup.Controls.Add(lengthLayout);

            // 4. Character Categories Group
            var charGroup = new GroupBox
            {
                Text = "Character Categories",
                Dock = DockStyle.Top,
                Height = 155,
                Padding = new Padding(20, 14, 20, 14),
                Margin = new Padding(0)
            };

            var charGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            charGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            charGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            charGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            charGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            _chkLower = new CheckBox { Text = "Lowercase (a-z)", Checked = true, Enabled = false, AutoSize = true, Dock = DockStyle.Fill };
            _chkUpper = new CheckBox { Text = "Uppercase (A-Z)", Checked = true, AutoSize = true, Dock = DockStyle.Fill };
            _chkNumbers = new CheckBox { Text = "Numbers (0-9)", Checked = true, AutoSize = true, Dock = DockStyle.Fill };
            _chkSymbols = new CheckBox { Text = "Symbols (!@#$...)", Checked = false, AutoSize = true, Dock = DockStyle.Fill };

            _chkUpper.CheckedChanged += delegate { GeneratePassword(); };
            _chkNumbers.CheckedChanged += delegate { GeneratePassword(); };
            _chkSymbols.CheckedChanged += delegate { GeneratePassword(); };

            charGrid.Controls.Add(_chkLower, 0, 0);
            charGrid.Controls.Add(_chkUpper, 1, 0);
            charGrid.Controls.Add(_chkNumbers, 0, 1);
            charGrid.Controls.Add(_chkSymbols, 1, 1);
            charGroup.Controls.Add(charGrid);

            mainLayout.Controls.Add(passwordPanel, 0, 0);
            mainLayout.Controls.Add(_lblStatus, 0, 1);
            mainLayout.Controls.Add(lengthGroup, 0, 2);
            mainLayout.Controls.Add(charGroup, 0, 3);

            Controls.Add(mainLayout);
            GeneratePassword();
        }

        private int GetUnbiasedRandomInt(int maxExclusive)
        {
            uint max = (uint)maxExclusive;
            uint threshold = (uint)(0x100000000UL % max);
            byte[] bytes = new byte[4];

            while (true)
            {
                _rng.GetBytes(bytes);
                uint val = BitConverter.ToUInt32(bytes, 0);
                if (val >= threshold)
                    return (int)(val % max);
            }
        }

        private void GeneratePassword()
        {
            _feedbackTimer.Stop();
            ResetCopyFeedback();

            int length = (int)_numLength.Value;
            var activeCategories = new List<string>(4) { LowercaseChars };

            if (_chkUpper.Checked) activeCategories.Add(UppercaseChars);
            if (_chkNumbers.Checked) activeCategories.Add(NumberChars);
            if (_chkSymbols.Checked) activeCategories.Add(SymbolChars);

            if (length < activeCategories.Count)
            {
                _isValidPassword = false;
                _txtPassword.Text = string.Empty;
                _btnCopy.Enabled = false;
                _lblStatus.ForeColor = Color.Firebrick;
                _lblStatus.Text = string.Format("Length must be at least {0} to include all selected categories.", activeCategories.Count);
                return;
            }

            _isValidPassword = true;
            _btnCopy.Enabled = true;

            string allChars = string.Concat(activeCategories);
            char[] buffer = new char[length];

            for (int i = 0; i < activeCategories.Count; i++)
            {
                string category = activeCategories[i];
                buffer[i] = category[GetUnbiasedRandomInt(category.Length)];
            }

            for (int i = activeCategories.Count; i < length; i++)
            {
                buffer[i] = allChars[GetUnbiasedRandomInt(allChars.Length)];
            }

            for (int i = buffer.Length - 1; i > 0; i--)
            {
                int j = GetUnbiasedRandomInt(i + 1);
                char temp = buffer[i];
                buffer[i] = buffer[j];
                buffer[j] = temp;
            }

            _txtPassword.Text = new string(buffer);
            _lblStatus.ForeColor = SystemColors.ControlDarkDark;
            _lblStatus.Text = string.Format("{0} characters | {1} active {2}", length, activeCategories.Count, activeCategories.Count == 1 ? "category" : "categories");
        }

        private void CopyPassword()
        {
            if (!_isValidPassword || string.IsNullOrEmpty(_txtPassword.Text))
                return;

            Clipboard.SetText(_txtPassword.Text);
            _btnCopy.Text = "Copied";
            _lblStatus.ForeColor = Color.SeaGreen;
            _lblStatus.Text = "Copied to clipboard.";

            _feedbackTimer.Stop();
            _feedbackTimer.Start();
        }

        private void ResetCopyFeedback()
        {
            _btnCopy.Text = "Copy";
            if (_isValidPassword)
            {
                int activeCount = 1 + (_chkUpper.Checked ? 1 : 0) + (_chkNumbers.Checked ? 1 : 0) + (_chkSymbols.Checked ? 1 : 0);
                _lblStatus.ForeColor = SystemColors.ControlDarkDark;
                _lblStatus.Text = string.Format("{0} characters | {1} active {2}", (int)_numLength.Value, activeCount, activeCount == 1 ? "category" : "categories");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _rng.Dispose();
                _feedbackTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}