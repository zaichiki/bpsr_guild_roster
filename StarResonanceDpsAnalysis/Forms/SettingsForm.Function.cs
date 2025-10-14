using SharpPcap;
using StarResonanceDpsAnalysis.Plugin;

namespace StarResonanceDpsAnalysis.Forms
{
    public partial class SettingsForm
    {
        /// <summary>
        /// 加载本机所有网卡到下拉框
        /// </summary>
        public void LoadDevices()
        {
            var devices = CaptureDeviceList.Instance;
            InterfaceComboBox.Items.Clear();
            foreach (var d in devices) InterfaceComboBox.Items.Add(d.Description);

            // 自动选择或按配置选择
            int targetIndex = (AppConfig.NetworkCard >= 0 && AppConfig.NetworkCard < devices.Count)
                ? AppConfig.NetworkCard
                : GetBestNetworkCardIndex(devices);

            if (targetIndex >= 0)
            {
                AppConfig.NetworkCard = targetIndex;
                InterfaceComboBox.SelectedIndex = targetIndex;
                Console.WriteLine($"选择网卡: {devices[targetIndex].Description} (索引: {targetIndex})");
            }
            else
            {
                Console.WriteLine("未找到可用网卡");
            }

            input1.Text = AppConfig.MouseThroughKey.ToString();
            input2.Text = AppConfig.FormTransparencyKey.ToString();
            input3.Text = AppConfig.WindowToggleKey.ToString();
            input4.Text = AppConfig.ClearDataKey.ToString();
            input5.Text = AppConfig.ClearHistoryKey.ToString();
        }


        private int GetBestNetworkCardIndex(CaptureDeviceList devices)
        {
            var active = System.Net.NetworkInformation.NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                             ni.GetIPProperties().UnicastAddresses.Any(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                .OrderByDescending(ni => ni.GetIPProperties().GatewayAddresses.Any()) // 网关优先
                .FirstOrDefault();

            if (active == null) return devices.Count > 0 ? 0 : -1;

            // 匹配分数最高的设备
            int bestIndex = -1, bestScore = -1;
            for (int i = 0; i < devices.Count; i++)
            {
                int score = 0;
                if (devices[i].Description.Contains(active.Name, StringComparison.OrdinalIgnoreCase)) score += 2;
                if (devices[i].Description.Contains(active.Description, StringComparison.OrdinalIgnoreCase)) score += 3;
                if (score > bestScore) { bestScore = score; bestIndex = i; }
            }
            return bestIndex;
        }
    }



}
