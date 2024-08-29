using System;
using System.Linq;
using System.Windows;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace WpfApp10
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadNetworkInterfaces();
            AutoIPAddressCheckBox.Checked += AutoIPAddressCheckBox_Checked;
            AutoIPAddressCheckBox.Unchecked += AutoIPAddressCheckBox_Unchecked;
            AutoDNSCheckBox.Checked += AutoDNSCheckBox_Checked;
            AutoDNSCheckBox.Unchecked += AutoDNSCheckBox_Unchecked;
        }

        private void LoadNetworkInterfaces()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                              nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Select(nic => new
                {
                    Name = nic.Name,
                    Description = $"{nic.Description} ({nic.OperationalStatus})",
                    Status = nic.OperationalStatus
                })
                .ToList();

            NetworkInterfaceComboBox.ItemsSource = interfaces;
            NetworkInterfaceComboBox.DisplayMemberPath = "Description";
            NetworkInterfaceComboBox.SelectedValuePath = "Name";
        }

        private void AutoIPAddressCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IPAddressTextBox.IsEnabled = false;
            SubnetMaskTextBox.IsEnabled = false;
            GatewayTextBox.IsEnabled = false;
        }

        private void AutoIPAddressCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            IPAddressTextBox.IsEnabled = true;
            SubnetMaskTextBox.IsEnabled = true;
            GatewayTextBox.IsEnabled = true;
        }

        private void AutoDNSCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PreferredDNSTextBox.IsEnabled = false;
            AlternateDNSTextBox.IsEnabled = false;
        }

        private void AutoDNSCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PreferredDNSTextBox.IsEnabled = true;
            AlternateDNSTextBox.IsEnabled = true;
        }

        private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
        {
            string interfaceName = NetworkInterfaceComboBox.SelectedValue?.ToString();

            if (string.IsNullOrEmpty(interfaceName))
            {
                MessageBox.Show("请在应用设置之前选择网络接口。", "info", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }
    
            // 根据是否选中获取IP决定IP设置
            if (AutoIPAddressCheckBox.IsChecked ?? false)
            {
                // 如果选中了获取IP, 那么IP设为自动
                ApplyNetworkSettings($"interface ip set address name=\"{interfaceName}\" source=dhcp");
            }
            else
            {
                // 如果没有选中获取IP, 手动设置IP
                string ipCommand = $"interface ip set address name=\"{interfaceName}\" static {IPAddressTextBox.Text} {SubnetMaskTextBox.Text} {GatewayTextBox.Text}";
                ApplyNetworkSettings(ipCommand);
            }

            // 只有当 IP 和 DNS 都勾选了自动获取时，才设置DNS为DHCP
            if ((AutoIPAddressCheckBox.IsChecked ?? false) && (AutoDNSCheckBox.IsChecked ?? false))
            {
                ApplyNetworkSettings($"interface ip set dns name=\"{interfaceName}\" source=dhcp");
            }
            else
            {
                // 其他情况下，手动设置DNS为输入框中的值
                string dnsCommand = $"interface ip set dns name=\"{interfaceName}\" static {PreferredDNSTextBox.Text} primary";
                ApplyNetworkSettings(dnsCommand);

                if (!string.IsNullOrEmpty(AlternateDNSTextBox.Text))
                {
                    ApplyNetworkSettings($"interface ip add dns name=\"{interfaceName}\" addr={AlternateDNSTextBox.Text} index=2");
                }
            }
        }



        private void ApplyNetworkSettings(string netshCommand)
        {
            Console.Out.WriteLine(netshCommand);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = netshCommand,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            MessageBox.Show($"Command executed: {netshCommand}\nOutput: {output}\nErrors: {errors}");
        }
    }
}
