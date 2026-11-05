using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Net;

namespace QuickZh {
    static class Program {
        public static Icon AppIcon;
        public static EventWaitHandle ShowEvent;
        [STAThread]
        static int Main(string[] args) {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 10;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool created;
            using (var mutex = new Mutex(true, "Local\\QuickZhScreenshot-v1-" + Environment.UserName, out created))
            using (ShowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QuickZhScreenshot-show-" + Environment.UserName)) {
                if (!created) { if (Array.IndexOf(args, "--background") < 0) ShowEvent.Set(); return 0; }
                AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                try {
                    var settings = Settings.Load(); settings.Validate(false);
                    Application.Run(new MainForm(settings, Array.IndexOf(args, "--background") >= 0));
                    return 0;
                } catch (Exception ex) {
                    MessageBox.Show("程序未能启动：" + ex.Message + "\n\n设置文件位置：" + Paths.Config, "快译截图", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1;
                } finally { if (AppIcon != null) AppIcon.Dispose(); }
            }
        }
    }
}
