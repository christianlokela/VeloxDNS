using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace VeloxDNS_Complete
{
    public class DnsProfile
    {
        public string Name { get; set; }
        public string IPv4_1 { get; set; }
        public string IPv4_2 { get; set; }
        public string IPv6_1 { get; set; }
        public string IPv6_2 { get; set; }
    }

    public partial class MainWindow : Window
    {
        private string profileFile = "profiles.json";
        private List<DnsProfile> profiles = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadAdapters();
            LoadProfiles();
        }

        private void LoadAdapters()
        {
            AdapterComboBox.Items.Clear();
            AdapterComboBox.Items.Add("Alle Adapter");

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    AdapterComboBox.Items.Add(ni.Name);
                }
            }

            AdapterComboBox.SelectedIndex = 0;
        }

        private string[] GetSelectedAdapters()
        {
            if (AdapterComboBox.SelectedItem?.ToString() == "Alle Adapter")
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(n => n.Name).ToArray();
            }
            else
            {
                return new string[] { AdapterComboBox.SelectedItem?.ToString() ?? "" };
            }
        }

        private void RunNetsh(string args)
        {
            var psi = new ProcessStartInfo("netsh", args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc.WaitForExit();
        }

        private void SetDns_Click(object sender, RoutedEventArgs e)
        {
            string[] adapters = GetSelectedAdapters();
            foreach (string adapter in adapters)
            {
                RunNetsh($"interface ip set dns name=\"{adapter}\" static {IPv4Dns1Box.Text.Trim()}");
                if (!string.IsNullOrWhiteSpace(IPv4Dns2Box.Text))
                    RunNetsh($"interface ip add dns name=\"{adapter}\" {IPv4Dns2Box.Text.Trim()} index=2");

                if (!string.IsNullOrWhiteSpace(IPv6Dns1Box.Text))
                    RunNetsh($"interface ipv6 set dnsservers name=\"{adapter}\" static {IPv6Dns1Box.Text.Trim()} primary");

                if (!string.IsNullOrWhiteSpace(IPv6Dns2Box.Text))
                    RunNetsh($"interface ipv6 add dnsservers name=\"{adapter}\" {IPv6Dns2Box.Text.Trim()} index=2");
            }

            MessageBox.Show("DNS gesetzt.");
        }

        private void SetAuto_Click(object sender, RoutedEventArgs e)
        {
            string[] adapters = GetSelectedAdapters();
            foreach (string adapter in adapters)
            {
                RunNetsh($"interface ip set dns name=\"{adapter}\" source=dhcp");
                RunNetsh($"interface ipv6 set dnsservers name=\"{adapter}\" source=dhcp");
            }

            MessageBox.Show("DNS zurückgesetzt.");
        }

        private void ShowAdapterInfo_Click(object sender, RoutedEventArgs e)
        {
            string selected = AdapterComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selected) || selected == "Alle Adapter")
            {
                MessageBox.Show("Bitte einen spezifischen Adapter auswählen.");
                return;
            }

            var ni = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Name == selected);
            if (ni == null)
            {
                MessageBox.Show("Adapter nicht gefunden.");
                return;
            }

            var props = ni.GetIPProperties();
            var sb = new StringBuilder();
            sb.AppendLine($"Adapter: {ni.Name}");
            sb.AppendLine($"Status: {ni.OperationalStatus}");
            sb.AppendLine($"MAC-Adresse: {ni.GetPhysicalAddress()}");

            var ipv4 = props.UnicastAddresses.FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 != null)
                sb.AppendLine($"IPv4: {ipv4.Address}");

            var ipv6 = props.UnicastAddresses.FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetworkV6);
            if (ipv6 != null)
                sb.AppendLine($"IPv6: {ipv6.Address}");

            var dns4 = props.DnsAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();
            var dns6 = props.DnsAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6).ToList();

            sb.AppendLine("DNS (IPv4): " + (dns4.Count > 0 ? string.Join(", ", dns4) : "Keine"));
            sb.AppendLine("DNS (IPv6): " + (dns6.Count > 0 ? string.Join(", ", dns6) : "Keine"));

            MessageBox.Show(sb.ToString(), "Adapter-Informationen");
        }

        private void SaveProfiles()
        {
            try
            {
                var json = JsonSerializer.Serialize(profiles);
                File.WriteAllText(profileFile, json);
            }
            catch { }
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            var name = NewProfileNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var existing = profiles.FirstOrDefault(p => p.Name == name);
            if (existing != null) profiles.Remove(existing);

            profiles.Add(new DnsProfile
            {
                Name = name,
                IPv4_1 = IPv4Dns1Box.Text.Trim(),
                IPv4_2 = IPv4Dns2Box.Text.Trim(),
                IPv6_1 = IPv6Dns1Box.Text.Trim(),
                IPv6_2 = IPv6Dns2Box.Text.Trim()
            });

            SaveProfiles();
            LoadProfiles();
        }

        private void LoadProfiles()
        {
            if (File.Exists(profileFile))
            {
                try
                {
                    var json = File.ReadAllText(profileFile);
                    profiles = JsonSerializer.Deserialize<List<DnsProfile>>(json) ?? new List<DnsProfile>();
                }
                catch { }
            }

            ProfileComboBox.Items.Clear();
            foreach (var profile in profiles)
            {
                ProfileComboBox.Items.Add(profile.Name);
            }
        }

        private void LoadProfile_Click(object sender, RoutedEventArgs e)
        {
            var name = ProfileComboBox.SelectedItem?.ToString();
            var profile = profiles.FirstOrDefault(p => p.Name == name);
            if (profile != null)
            {
                IPv4Dns1Box.Text = profile.IPv4_1;
                IPv4Dns2Box.Text = profile.IPv4_2;
                IPv6Dns1Box.Text = profile.IPv6_1;
                IPv6Dns2Box.Text = profile.IPv6_2;
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var name = ProfileComboBox.SelectedItem?.ToString();
            var profile = profiles.FirstOrDefault(p => p.Name == name);
            if (profile != null)
            {
                profiles.Remove(profile);
                SaveProfiles();
                LoadProfiles();
            }
        }
    }
}
