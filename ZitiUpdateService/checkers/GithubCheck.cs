﻿using System;
using System.IO;
using System.Reflection;
using System.Net;
using System.Security.Cryptography;
using System.Collections.Generic;

using NLog;
using Newtonsoft.Json.Linq;

using ZitiDesktopEdge.Utility;
using ZitiUpdateService.Checkers.PeFile;

namespace ZitiUpdateService.Checkers {

	internal class GithubCheck : UpdateCheck {
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		string url;
		string releasesUrl;
		string downloadUrl = null;
		Version nextVersion = null;
		

		public GithubCheck(Version compareTo, string url, string releasesUrl) : base(compareTo) {
			this.url = url;
			this.releasesUrl = releasesUrl;
			Avail = CheckUpdate(compareTo);
		}

		override public bool AlreadyDownloaded(string destinationFolder, string destinationName) {
			return File.Exists(Path.Combine(destinationFolder, destinationName));
		}

		override public void CopyUpdatePackage(string destinationFolder, string destinationName) {
			WebClient webClient = new WebClient();
			string dest = Path.Combine(destinationFolder, destinationName);
			Logger.Info("download started for: {0} to {1}", downloadUrl, dest);
			webClient.DownloadFile(downloadUrl, dest);
			Logger.Info("download complete to: {0}", dest);
		}

		private int CheckUpdate(Version currentVersion) {
			Logger.Debug("checking for update begins. current version detected as {0}", currentVersion);
			Logger.Debug("issuing http get to url: {0}", url);
			JObject json = GithubAPI.GetJson(url);

			JArray assets = JArray.Parse(json.Property("assets").Value.ToString());
			foreach (JObject asset in assets.Children<JObject>()) {
				string assetName = asset.Property("name").Value.ToString();

				if (assetName.StartsWith("Ziti.Desktop.Edge.Client-")) {
					downloadUrl = asset.Property("browser_download_url").Value.ToString();
					break;
				} else {
					Logger.Debug("skipping asset with name: {assetName}", assetName);
				}
			}

			if (downloadUrl == null) {
				Logger.Error("DOWNLOAD URL not found at: {0}", url);
				return 0;
			}
			Logger.Debug("download url detected: {0}", downloadUrl);
			FileName = downloadUrl.Substring(downloadUrl.LastIndexOf('/') + 1);
			Logger.Debug("download file name: {0}", FileName);

			string releaseVersion = json.Property("tag_name").Value.ToString();
			string releaseName = json.Property("name").Value.ToString();
			nextVersion = VersionUtil.NormalizeVersion(new Version(releaseVersion));
			string isoPublishedDate = json.Property("published_at").Value.ToString();
			PublishDate = DateTime.Parse(isoPublishedDate, null, System.Globalization.DateTimeStyles.RoundtripKind);


			int compare = currentVersion.CompareTo(nextVersion);
			if (compare < 0) {
				Logger.Info("upgrade {} is available. Published version: {} is newer than the current version: {}", releaseName, nextVersion, currentVersion);
			} else if (compare > 0) {
				Logger.Info("the version installed: {0} is newer than the released version: {1}", currentVersion, nextVersion);
			}
			return compare;
		}

		override public bool HashIsValid(string destinationFolder, string destinationName) {
			WebClient webClient = new WebClient();
			string sha256dest = Path.Combine(destinationFolder, destinationName + ".sha256");
			string downloadUrlsha256 = downloadUrl + ".sha256";
			Logger.Info("download started for: {0} to {1}", downloadUrlsha256, sha256dest);
			webClient.DownloadFile(downloadUrlsha256, sha256dest);
			Logger.Info("download complete to: {0}", sha256dest);

			string dest = Path.Combine(destinationFolder, destinationName);
			string hash = File.ReadAllText(sha256dest);

			using (SHA256 hasher = SHA256.Create())
			using (FileStream stream = File.OpenRead(dest)) {
				byte[] sha256bytes = hasher.ComputeHash(stream);
				string computed = BitConverter.ToString(sha256bytes).Replace("-", "");

				File.Delete(sha256dest);
				Logger.Info("comparing computed hash: {0} to downloaded hash: {1}", computed, hash);
				return computed.ToLower().Trim() == hash.ToLower().Trim();
			}
        }

		override public Version GetNextVersion() {
			return nextVersion;
		}

		private void getReleaseInfoAfterCurrent(string releaseUrl, Version currentVersion, out DateTime _publishedDateAfterCurrent, out Version _versionAfterCurrent ) {
			Logger.Debug("Fetching the releases info from {0}", releaseUrl);
			JArray jArray = GithubAPI.GetJsonArray(releaseUrl);
			string isoPublishedDate = null;
			Version publishedReleaseVersion = null;

			if (jArray.HasValues) {
				foreach(JObject json in jArray.Children<JObject>()) {
					string releaseVersion = json.Property("name").Value.ToString();
					Version normalizedReleaseVersion = null;
					try {
						normalizedReleaseVersion = VersionUtil.NormalizeVersion(new Version(releaseVersion));
					} catch (Exception e) {
						try {
							releaseVersion = json.Property("tag_name").Value.ToString();
							normalizedReleaseVersion = VersionUtil.NormalizeVersion(new Version(releaseVersion));
						} catch (Exception err) {
							Logger.Error("Cound not fetch version from name due to {0} and tag_name due to {1}", e.Message, err.Message);
							continue;
						}
					}

					if (normalizedReleaseVersion.CompareTo(currentVersion) <= 0) {
						break;
					}
					isoPublishedDate = json.Property("published_at").Value.ToString();
					publishedReleaseVersion = normalizedReleaseVersion;
				}
			}
			if (isoPublishedDate != null) {
				_publishedDateAfterCurrent = DateTime.Parse(isoPublishedDate, null, System.Globalization.DateTimeStyles.RoundtripKind);
			} else {
				_publishedDateAfterCurrent = DateTime.Now;
			}
			_versionAfterCurrent = publishedReleaseVersion;

		}

		private DateTime getCreationTime(string publishedDateStr, string fileDestination) {
			DateTime publishedDate = DateTime.Now;

			try {
				DateTime.TryParse(publishedDateStr, out publishedDate);
			} catch (Exception e) {
				Logger.Error("Could not convert published date of the installer - input string : {0} due to {1}. Fetching download time instead.", publishedDateStr, e.Message);
				try {
					if (fileDestination != null) {
						publishedDate = File.GetCreationTime(fileDestination);
					}
				} catch (Exception err) {
					Logger.Error("Could not fetch creation date of the installer due to {0}.", err.Message);
				}

			}
			return publishedDate;
		}
	}
}
