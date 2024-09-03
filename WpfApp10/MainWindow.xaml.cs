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
                .Where(nic => (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet && !nic.Description.Contains("Virtual")) ||
                              (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && !nic.Description.Contains("Virtual")))
                .Select(nic => new
                {
                    Name = nic.Name,
                    Description = $"{nic.Name} - {nic.Description} ({nic.OperationalStatus})",
                    // Description = $"{nic.Name}",
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

      private async Task<(string output, string error)> ApplyNetworkSettingsAsync(string netshCommand)
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
    string output = await process.StandardOutput.ReadToEndAsync();
    string error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    return (output, error);
}

private async void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
{
    string interfaceName = NetworkInterfaceComboBox.SelectedValue?.ToString();

    if (string.IsNullOrEmpty(interfaceName))
    {
        MessageBox.Show("请在应用设置之前选择网络接口。", "info", MessageBoxButton.OK, MessageBoxImage.None);
        return;
    }

    // 正则表达式验证IP地址和DNS地址的格式
    string ipPattern = @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\." +
                       @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\." +
                       @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\." +
                       @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

    if (!(AutoIPAddressCheckBox.IsChecked ?? false))
    {
        // 验证 IP 地址、子网掩码和网关
        if (!System.Text.RegularExpressions.Regex.IsMatch(IPAddressTextBox.Text, ipPattern) ||
            !System.Text.RegularExpressions.Regex.IsMatch(SubnetMaskTextBox.Text, ipPattern) ||
            !System.Text.RegularExpressions.Regex.IsMatch(GatewayTextBox.Text, ipPattern))
        {
            MessageBox.Show("请输入有效的 IP 地址、子网掩码和网关。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
    }

    // 验证 DNS 服务器地址
    if (!(AutoDNSCheckBox.IsChecked ?? false))
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(PreferredDNSTextBox.Text, ipPattern) ||
            (!string.IsNullOrEmpty(AlternateDNSTextBox.Text) && !System.Text.RegularExpressions.Regex.IsMatch(AlternateDNSTextBox.Text, ipPattern)))
        {
            MessageBox.Show("请输入有效的 DNS 服务器地址。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
    }

    List<string> outputs = new List<string>();
    List<string> errors = new List<string>();

    if (AutoIPAddressCheckBox.IsChecked ?? false)
    {
        var (output, error) = await ApplyNetworkSettingsAsync($"interface ip set address name=\"{interfaceName}\" source=dhcp");
        outputs.Add(output);
        errors.Add(error);
    }
    else
    {
        string ipCommand = $"interface ip set address name=\"{interfaceName}\" static {IPAddressTextBox.Text} {SubnetMaskTextBox.Text} {GatewayTextBox.Text}";
        var (output, error) = await ApplyNetworkSettingsAsync(ipCommand);
        outputs.Add(output);
        errors.Add(error);
    }

    if ((AutoIPAddressCheckBox.IsChecked ?? false) && (AutoDNSCheckBox.IsChecked ?? false))
    {
        var (output, error) = await ApplyNetworkSettingsAsync($"interface ip set dns name=\"{interfaceName}\" source=dhcp");
        outputs.Add(output);
        errors.Add(error);
    }
    else
    {
        string dnsCommand = $"interface ip set dns name=\"{interfaceName}\" static {PreferredDNSTextBox.Text} primary";
        var (output, error) = await ApplyNetworkSettingsAsync(dnsCommand);
        outputs.Add(output);
        errors.Add(error);

        if (!string.IsNullOrEmpty(AlternateDNSTextBox.Text))
        {
            var (output2, error2) = await ApplyNetworkSettingsAsync($"interface ip add dns name=\"{interfaceName}\" addr={AlternateDNSTextBox.Text} index=2");
            outputs.Add(output2);
            errors.Add(error2);
        }
    }

    if (errors.All(string.IsNullOrEmpty))
    {
        if (outputs.All(string.IsNullOrEmpty))
        {
            MessageBox.Show("设置成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"设置成功，提示信息：\n{string.Join("\n", outputs.Where(o => !string.IsNullOrEmpty(o)))}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    else
    {
        MessageBox.Show($"设置过程中发生错误：\n{string.Join("\n", errors.Where(e => !string.IsNullOrEmpty(e)))}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}



        // private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
        // {
        //     string interfaceName = NetworkInterfaceComboBox.SelectedValue?.ToString();
        //
        //     if (string.IsNullOrEmpty(interfaceName))
        //     {
        //         MessageBox.Show("请在应用设置之前选择网络接口。", "info", MessageBoxButton.OK, MessageBoxImage.None);
        //         return;
        //     }
        //
        //     // 正则表达式验证IP地址和DNS地址的格式
        //     string ipPattern = @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\." +
        //                        @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\." +
        //                        @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\." +
        //                        @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
        //
        //     if (!(AutoIPAddressCheckBox.IsChecked ?? false))
        //     {
        //         // 验证 IP 地址、子网掩码和网关
        //         if (!System.Text.RegularExpressions.Regex.IsMatch(IPAddressTextBox.Text, ipPattern) ||
        //             !System.Text.RegularExpressions.Regex.IsMatch(SubnetMaskTextBox.Text, ipPattern) ||
        //             !System.Text.RegularExpressions.Regex.IsMatch(GatewayTextBox.Text, ipPattern))
        //         {
        //             MessageBox.Show("请输入有效的 IP 地址、子网掩码和网关。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //             return;
        //         }
        //     }
        //
        //     // 验证 DNS 服务器地址
        //     if (!(AutoDNSCheckBox.IsChecked ?? false))
        //     {
        //         if (!System.Text.RegularExpressions.Regex.IsMatch(PreferredDNSTextBox.Text, ipPattern) ||
        //             (!string.IsNullOrEmpty(AlternateDNSTextBox.Text) &&
        //              !System.Text.RegularExpressions.Regex.IsMatch(AlternateDNSTextBox.Text, ipPattern)))
        //         {
        //             MessageBox.Show("请输入有效的 DNS 服务器地址。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //             return;
        //         }
        //     }
        //
        //     // 根据是否选中获取IP决定IP设置
        //     if (AutoIPAddressCheckBox.IsChecked ?? false)
        //     {
        //         // 如果选中了获取IP, 那么IP设为自动
        //         ApplyNetworkSettings($"interface ip set address name=\"{interfaceName}\" source=dhcp");
        //     }
        //     else
        //     {
        //         // 如果没有选中获取IP, 手动设置IP
        //         string ipCommand =
        //             $"interface ip set address name=\"{interfaceName}\" static {IPAddressTextBox.Text} {SubnetMaskTextBox.Text} {GatewayTextBox.Text}";
        //         ApplyNetworkSettings(ipCommand);
        //     }
        //
        //     // 只有当 IP 和 DNS 都勾选了自动获取时，才设置DNS为DHCP
        //     if ((AutoIPAddressCheckBox.IsChecked ?? false) && (AutoDNSCheckBox.IsChecked ?? false))
        //     {
        //         ApplyNetworkSettings($"interface ip set dns name=\"{interfaceName}\" source=dhcp");
        //     }
        //     else
        //     {
        //         // 其他情况下，手动设置DNS为输入框中的值
        //         string dnsCommand =
        //             $"interface ip set dns name=\"{interfaceName}\" static {PreferredDNSTextBox.Text} primary";
        //         ApplyNetworkSettings(dnsCommand);
        //
        //         if (!string.IsNullOrEmpty(AlternateDNSTextBox.Text))
        //         {
        //             ApplyNetworkSettings(
        //                 $"interface ip add dns name=\"{interfaceName}\" addr={AlternateDNSTextBox.Text} index=2");
        //         }
        //     }
        // }


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