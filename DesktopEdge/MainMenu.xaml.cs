using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System;
using System.Reflection;
using System.Net.Mail;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using ZitiDesktopEdge.ServiceClient;

using NLog;
using ZitiDesktopEdge.DataStructures;
using ZitiDesktopEdge.Native;

namespace ZitiDesktopEdge
{	
    /// <summary>
    /// Interaction logic for MainMenu.xaml
    /// </summary>
    public partial class MainMenu : UserControl {
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		public delegate void AttachementChanged(bool attached);
		public event AttachementChanged OnAttachmentChange;
		public delegate void LogLevelChanged(string level);
		public event LogLevelChanged OnLogLevelChanged;
		public delegate void Detched(MouseButtonEventArgs e);
		public event Detched OnDetach;
		public string menuState = "Main";
		public string licenseData = "it's open source.";
		public string LogLevel = "";
		private string _updateUrl = "https://api.github.com/repos/openziti/desktop-edge-win/releases/latest";
		private string _downloadUrl = "";

		private string appVersion = null;
		public MainMenu() {
            InitializeComponent();

			appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			LicensesItems.Text = licenseData;
			CheckUpdates();
		}

		private void HideMenu(object sender, MouseButtonEventArgs e) {
			menuState = "Menu";
			UpdateState();
			MainMenuArea.Visibility = Visibility.Collapsed;
		}
		private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left) {
				OnDetach(e);
			}
		}

		private void CloseApp(object sender, MouseButtonEventArgs e) {
			Application.Current.Shutdown();
		}

		private void DoUpdate(object sender, MouseButtonEventArgs e) {
			Process.Start(_downloadUrl);
		}

		private void ShowAbout(object sender, MouseButtonEventArgs e) {
			menuState = "About";
			UpdateState();
		}

		private void ShowAdvanced(object sender, MouseButtonEventArgs e) {
			menuState = "Advanced";
			UpdateState();
		}
		private void ShowLicenses(object sender, MouseButtonEventArgs e) {
			menuState = "Licenses";
			UpdateState();
		}
		private void ShowConfig(object sender, MouseButtonEventArgs e) {
			menuState = "Config";
			UpdateState();
		}
		private void ShowLogs(object sender, MouseButtonEventArgs e) {
			menuState = "Logs";
			UpdateState();
		}
		private void ShowUILogs(object sender, MouseButtonEventArgs e) {
			menuState = "UILogs";
			UpdateState();
		}
		private void SetLogLevel(object sender, MouseButtonEventArgs e) {
			menuState = "LogLevel";
			UpdateState();
		}

		private void CheckUpdates() {
			try
			{
				HttpWebRequest httpWebRequest = WebRequest.CreateHttp(_updateUrl);
				httpWebRequest.Method = "GET";
				httpWebRequest.ContentType = "application/json";
				httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.103 Safari/537.36";
				HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
				StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream());
				string result = streamReader.ReadToEnd();
				JObject json = JObject.Parse(result);
				string serverVersion = json.Property("tag_name").Value.ToString() + ".0";

				Version installed = new Version(appVersion);
				Version published = new Version(serverVersion);
				int compare = installed.CompareTo(published);
				if (compare < 0)
				{
					UpdateAvailable.Content = "An Upgrade is available, click to download";
					UpdateAvailable.Visibility = Visibility.Visible;
				}
				else if (compare > 0)
				{
					UpdateAvailable.Content = "Your version is newer than the released version";
					UpdateAvailable.Visibility = Visibility.Visible;
				}
				JArray assets = JArray.Parse(json.Property("assets").Value.ToString());
				foreach (JObject asset in assets.Children<JObject>())
				{
					_downloadUrl = asset.Property("browser_download_url").Value.ToString();
					break;
				}
			} catch(Exception ex)
			{
				UpdateAvailable.Content = "An exception occurred while performing upgrade check";
				logger.Error(ex, "Error when checking for version: " + ex.Message);
				UpdateAvailable.Visibility = Visibility.Visible;
			}
		}

		private void UpdateState() {
			MainItems.Visibility = Visibility.Collapsed;
			AboutItems.Visibility = Visibility.Collapsed;
			MainItemsButton.Visibility = Visibility.Collapsed;
			AboutItemsArea.Visibility = Visibility.Collapsed;
			BackArrow.Visibility = Visibility.Collapsed;
			AdvancedItems.Visibility = Visibility.Collapsed;
			LicensesItems.Visibility = Visibility.Collapsed;
			LogsItems.Visibility = Visibility.Collapsed;
			ConfigItems.Visibility = Visibility.Collapsed;
			LogLevelItems.Visibility = Visibility.Collapsed;

			if (menuState == "About") {
				MenuTitle.Content = "About";
				AboutItemsArea.Visibility = Visibility.Visible;
				AboutItems.Visibility = Visibility.Visible;
				BackArrow.Visibility = Visibility.Visible;

				string version = "";
				try {
					TunnelStatus s = (TunnelStatus)Application.Current.Properties["CurrentTunnelStatus"];
					version = $"{s.ServiceVersion.Version}@{s.ServiceVersion.Revision}";
				} catch (Exception e) {
					logger.Warn(e, "Could not get service version/revision?");
				}

				// Interface Version
				VersionInfo.Content = $"App: {appVersion} Service: {version}";

			} else if (menuState=="Advanced") {
				MenuTitle.Content = "Advanced Settings";
				AdvancedItems.Visibility = Visibility.Visible;
				BackArrow.Visibility = Visibility.Visible;
			} else if (menuState=="Licenses") {
				MenuTitle.Content = "Third Party Licenses";
				LicensesItems.Visibility = Visibility.Visible;
				BackArrow.Visibility = Visibility.Visible;
			} else if (menuState=="Logs") {
				MenuTitle.Content = "Advanced Settings";
				AdvancedItems.Visibility = Visibility.Visible;
				//string targetFile = NativeMethods.GetFinalPathName(MainWindow.ExpectedLogPathServices);
				string targetFile = MainWindow.ExpectedLogPathServices;

				OpenLogFile("service", targetFile);
				BackArrow.Visibility = Visibility.Visible;
			} else if (menuState == "UILogs") {
				MenuTitle.Content = "Advanced Settings";
				AdvancedItems.Visibility = Visibility.Visible;
				OpenLogFile("UI", MainWindow.ExpectedLogPathUI);
				BackArrow.Visibility = Visibility.Visible;
			} else if (menuState == "LogLevel") {
				ResetLevels();

				MenuTitle.Content = "Set Log Level";
				LogLevelItems.Visibility = Visibility.Visible;
				BackArrow.Visibility = Visibility.Visible;
			} else if (menuState=="Config") {
				MenuTitle.Content = "Tunnel Configuration";
				ConfigItems.Visibility = Visibility.Visible;
				BackArrow.Visibility = Visibility.Visible;
				
				ConfigIp.Value = Application.Current.Properties["ip"]?.ToString();
				ConfigSubnet.Value = Application.Current.Properties["subnet"]?.ToString();
				ConfigMtu.Value = Application.Current.Properties["mtu"]?.ToString();
				ConfigDns.Value = Application.Current.Properties["dns"]?.ToString();
			} else {
				MenuTitle.Content = "Main Menu";
				MainItems.Visibility = Visibility.Visible;
				MainItemsButton.Visibility = Visibility.Visible;
			}
		}

		private void OpenLogFile(string which, string logFile) {
			var whichRoot = Path.Combine(MainWindow.ExpectedLogPathRoot, which);
			try {
				string target = NativeMethods.GetFinalPathName(logFile);
				if (File.Exists(target)) {
					logger.Info("opening {0} logs at: {1}", which, target);
					var p = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
					if (p != null) {
						logger.Info("showing {0} logs. file: {1}", which, target);
					} else {
						Process.Start(whichRoot);
					}
					return;
				} else {
					logger.Warn("could not show {0} logs. file not found: {1}", which, target);
				}
			} catch {
			}
			Process.Start(whichRoot);
		}

		private void GoBack(object sender, MouseButtonEventArgs e) {
			if (menuState=="Config"||menuState== "LogLevel" || menuState=="UILogs") {
				menuState = "Advanced";
			} else if (menuState=="Licenses") {
				menuState = "About";
			} else {
				menuState = "Menu";
			}
			UpdateState();
		}
		private void ShowPrivacy(object sender, MouseButtonEventArgs e) {
			Process.Start(new ProcessStartInfo("https://netfoundry.io/privacy") { UseShellExecute = true });
		}
		private void ShowTerms(object sender, MouseButtonEventArgs e) {
			Process.Start(new ProcessStartInfo("https://netfoundry.io/terms") { UseShellExecute = true });
		}

		async private void ShowFeedback(object sender, MouseButtonEventArgs e) {
			DataClient client = (DataClient)Application.Current.Properties["ServiceClient"];
			var mailMessage = new MailMessage("ziti-support@netfoundry.io", "ziti-support@netfoundry.io");
			mailMessage.Subject = "Ziti Support";
			mailMessage.IsBodyHtml = false;
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append("Logs collected at : " + DateTime.Now.ToString());
			sb.Append(". client version : " + appVersion);

			mailMessage.Body = sb.ToString();

			string timestamp = DateTime.Now.ToFileTime().ToString();
			var monitorClient = (MonitorClient)Application.Current.Properties["MonitorClient"];
			ServiceStatusEvent resp = await monitorClient.CaptureLogsAsync();
			string pathToLogs = resp.Message;
			logger.Info("Log files found at : {0}", resp.Message);
			mailMessage.Attachments.Add(new Attachment(pathToLogs));

			string emlFile = Path.Combine(Path.GetTempPath(), timestamp+"-ziti.eml");

			using (var filestream = File.Open(emlFile, FileMode.Create)) {
				var binaryWriter = new BinaryWriter(filestream);
				binaryWriter.Write(System.Text.Encoding.UTF8.GetBytes("X-Unsent: 1" + Environment.NewLine));
				var assembly = typeof(SmtpClient).Assembly;
				var mailWriterType = assembly.GetType("System.Net.Mail.MailWriter");
				var mailWriterContructor = mailWriterType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Stream) }, null);
				var mailWriter = mailWriterContructor.Invoke(new object[] { filestream });
				var sendMethod = typeof(MailMessage).GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic);
				sendMethod.Invoke(mailMessage, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { mailWriter, true, true }, null);
				var closeMethod = mailWriter.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic);
				closeMethod.Invoke(mailWriter, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { }, null);
			}

			var p = Process.Start(emlFile);
            p.Exited += (object lambdaSender, EventArgs lambdaEventArgs) => {
				logger.Info("Removing temp file: {0}", emlFile);
				File.Delete(emlFile);
			};
			p.EnableRaisingEvents = true;
		}

        private void ShowSupport(object sender, MouseButtonEventArgs e) {
			Process.Start(new ProcessStartInfo("https://openziti.discourse.group/") { UseShellExecute = true });
		}

		private void DetachWindow(object sender, MouseButtonEventArgs e) {
			Application.Current.MainWindow.ShowInTaskbar = true;
			DetachButton.Visibility = Visibility.Collapsed;
			AttachButton.Visibility = Visibility.Visible;
			Arrow.Visibility = Visibility.Collapsed;
			if (OnAttachmentChange != null) {
				OnAttachmentChange(false);
			}
			MainMenuArea.Visibility = Visibility.Collapsed;
		}

		public void Detach() {
			Application.Current.MainWindow.ShowInTaskbar = true;
			DetachButton.Visibility = Visibility.Collapsed;
			AttachButton.Visibility = Visibility.Visible;
			Arrow.Visibility = Visibility.Collapsed;
		}
		private void RetachWindow(object sender, MouseButtonEventArgs e) {
			Application.Current.MainWindow.ShowInTaskbar = false;
			DetachButton.Visibility = Visibility.Visible;
			AttachButton.Visibility = Visibility.Collapsed;
			Arrow.Visibility = Visibility.Visible;
			if (OnAttachmentChange != null) {
				OnAttachmentChange(true);
			}
		}

		private void ResetLevels() {
			if (this.LogLevel == "") this.LogLevel = "error";
			LogVerbose.IsSelected = false;
			LogDebug.IsSelected = false;
			LogInfo.IsSelected = false;
			LogError.IsSelected = false;
			LogFatal.IsSelected = false;
			LogWarn.IsSelected = false;
			LogTrace.IsSelected = false;
			if (this.LogLevel == "verbose") LogVerbose.IsSelected = true;
			else if (this.LogLevel == "debug") LogDebug.IsSelected = true;
			else if (this.LogLevel == "info") LogInfo.IsSelected = true;
			else if (this.LogLevel == "error") LogError.IsSelected = true;
			else if (this.LogLevel == "fatal") LogFatal.IsSelected = true;
			else if (this.LogLevel == "warn") LogWarn.IsSelected = true;
			else if (this.LogLevel == "trace") LogTrace.IsSelected = true;
		}

		private void SetLevel(object sender, MouseButtonEventArgs e) {
			SubOptionItem item = (SubOptionItem)sender;
			this.LogLevel = item.Label.ToLower();
			if (OnLogLevelChanged != null) {
				OnLogLevelChanged(this.LogLevel);
			}
			ResetLevels();
		}
	}
}
