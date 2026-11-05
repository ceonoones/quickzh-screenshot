using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace QuickZh {
    public sealed class SettingsForm : Form {
        private TextBox url, key, model, prompt, hotkey;
        private ComboBox language; private NumericUpDown timeout;
        private CheckBox autoStart, minimized, copyImage, saveImage, streaming, disableThinking;
        private Label testStatus; private Button testButton, saveButton;
        private Settings initial; private CancellationTokenSource testCancellation;
        public Settings Result { get; private set; }
        public bool AutoStart { get { return autoStart.Checked; } }
        private readonly Func<Settings, bool, string> save;
        public SettingsForm(Settings settings, Func<Settings, bool, string> saveAction) {
            initial = settings.Clone(); save = saveAction;
            Text = "设置 · 快译截图"; Icon = Program.AppIcon; Font = new Font("Microsoft YaHei UI", 9.5F);
            AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(760, 748); MinimumSize = new Size(670, 600);
            StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Background; ShowInTaskbar = false;
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 65, Padding = new Padding(20, 10, 10, 6), FlowDirection = FlowDirection.RightToLeft, BackColor = Color.White };
            saveButton = Theme.Button("保存设置", SaveSettings, true); footer.Controls.Add(saveButton);
            footer.Controls.Add(Theme.Button("取消", (s,e) => Close(), false)); Controls.Add(footer);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22, 16, 22, 10) }; Controls.Add(scroll); scroll.BringToFront();
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 12, 0) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); scroll.Controls.Add(table);
            Action<string, Control> row = (label, control) => {
                int n = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                table.Controls.Add(Theme.Label(label), 0, n); control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 4, 0, 9); table.Controls.Add(control, 1, n);
            };
            Action<string> note = text => {
                int n = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var l = new Label { Text = text, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 12), MaximumSize = new Size(530, 0) };
                table.Controls.Add(l, 1, n);
            };
            url = new TextBox { Text = settings.ApiUrl }; row("API 地址", url);
            note("支持 OpenAI 兼容接口；可填站点根地址、/v1 或完整 /chat/completions 地址。");
            key = new TextBox { Text = settings.ApiKey, UseSystemPasswordChar = true }; row("API Key", key);
            var showKey = new CheckBox { Text = "显示 API Key（本机明文保存）", AutoSize = true }; showKey.CheckedChanged += (s,e) => key.UseSystemPasswordChar = !showKey.Checked; row("", showKey);
            model = new TextBox { Text = settings.Model }; row("模型名称", model);
            note("优先选择服务商提供的非思考模型以获得更快速度。模型名称以你的服务商为准。");
            streaming = new CheckBox { Text = "流式显示译文（网关故障时自动尝试普通请求）", Checked = settings.UseStreaming, AutoSize = true }; row("速度与兼容", streaming);
            disableThinking = new CheckBox { Text = "关闭深度思考，优先快速翻译", Checked = settings.DisableThinking, AutoSize = true }; row("", disableThinking);
            note("关闭深度思考需要服务商支持 thinking 参数；如接口不兼容，可取消此选项。");
            prompt = new TextBox { Text = settings.Prompt, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 140, AcceptsReturn = true }; row("翻译提示词", prompt);
            var reset = Theme.Button("恢复默认提示词", (s,e) => prompt.Text = Settings.DefaultPrompt, false); row("", reset);
            language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var item in Ocr.Languages) language.Items.Add(new LanguageItem(item.Key, item.Value));
            for (int i = 0; i < language.Items.Count; i++) if (((LanguageItem)language.Items[i]).Code == settings.Language) language.SelectedIndex = i;
            row("文字识别语言", language); note("默认识别英语和俄语。其他语言请在这里切换；准确选择语言有助于提高速度和识别率。");
            hotkey = new TextBox { Text = settings.Hotkey, ReadOnly = true, BackColor = Color.White };
            hotkey.KeyDown += (s,e) => {
                e.SuppressKeyPress = true; e.Handled = true;
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu) return;
                if (!e.Control && !e.Alt) return;
                hotkey.Text = (e.Control ? "Ctrl+" : "") + (e.Alt ? "Alt+" : "") + (e.Shift ? "Shift+" : "") + e.KeyCode;
            }; row("截图快捷键", hotkey); note("点击输入框后按下组合键，例如 Ctrl+Alt+Q。快捷键冲突会在保存时提示。");
            timeout = new NumericUpDown { Minimum = 10, Maximum = 300, Value = Math.Max(10, Math.Min(300, settings.TimeoutSeconds)) }; row("超时（秒）", timeout);
            autoStart = new CheckBox { Text = "开机自动启动（登录后直接进入系统托盘）", Checked = Startup.Enabled, AutoSize = true }; row("启动", autoStart);
            minimized = new CheckBox { Text = "手动打开程序时也直接进入系统托盘", Checked = settings.StartMinimized, AutoSize = true }; row("", minimized);
            copyImage = new CheckBox { Text = "截图完成后自动复制图片到剪贴板", Checked = settings.AutoCopyImage, AutoSize = true }; row("截图", copyImage);
            saveImage = new CheckBox { Text = "截图完成后自动将 PNG 保存到桌面", Checked = settings.AutoSaveImage, AutoSize = true }; row("", saveImage);
            testButton = Theme.Button("测试 API 连接", TestApi, false); row("连接测试", testButton);
            testStatus = new Label { Text = "使用一句示例英文测试，不读取屏幕内容。", AutoSize = true, ForeColor = Theme.Muted, MaximumSize = new Size(530, 0) }; row("", testStatus);
            note("本机 OCR 离线运行；翻译时仅向上述 API 地址发送原文和提示词，不发送截图。\n设置文件：" + Paths.Config);
            FormClosing += (s,e) => { if (testCancellation != null) testCancellation.Cancel(); };
        }
        private Settings ReadSettings() {
            var s = initial.Clone(); s.ApiUrl = url.Text.Trim(); s.ApiKey = key.Text.Trim(); s.Model = model.Text.Trim();
            s.Prompt = prompt.Text.Trim(); s.Language = language.SelectedItem == null ? "eng+rus" : ((LanguageItem)language.SelectedItem).Code;
            s.Hotkey = hotkey.Text; s.StartMinimized = minimized.Checked; s.AutoCopyImage = copyImage.Checked; s.AutoSaveImage = saveImage.Checked; s.TimeoutSeconds = (int)timeout.Value;
            s.UseStreaming = streaming.Checked; s.DisableThinking = disableThinking.Checked;
            return s;
        }
        private void SaveSettings(object sender, EventArgs e) {
            try {
                var s = ReadSettings(); s.Validate(false);
                string error = save(s, autoStart.Checked);
                if (error != null) { MessageBox.Show(this, error, "设置未保存", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                Result = s; DialogResult = DialogResult.OK; Close();
            } catch (Exception ex) { MessageBox.Show(this, ex.Message, "设置未保存", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        private async void TestApi(object sender, EventArgs e) {
            testCancellation = new CancellationTokenSource(); var current = testCancellation;
            testButton.Enabled = false; saveButton.Enabled = false; testStatus.Text = "正在测试…";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try {
                var s = ReadSettings(); s.Validate(true);
                string result = await ApiClient.Translate(s, "Hello! Have a nice day.", null, current.Token);
                if (!IsDisposed) { testStatus.ForeColor = Color.FromArgb(22, 133, 86); testStatus.Text = "连接成功（" + watch.Elapsed.TotalSeconds.ToString("0.0") + " 秒）：" + result; }
            } catch (Exception ex) { if (!IsDisposed) { testStatus.ForeColor = Color.Firebrick; testStatus.Text = ex is OperationCanceledException ? "测试已停止。" : ex.Message; } }
            finally { if (!IsDisposed) { testButton.Enabled = true; saveButton.Enabled = true; } current.Dispose(); if (testCancellation == current) testCancellation = null; }
        }
        private sealed class LanguageItem {
            public string Code; private string name;
            public LanguageItem(string c, string n) { Code = c; name = n; }
            public override string ToString() { return name; }
        }
    }
}
