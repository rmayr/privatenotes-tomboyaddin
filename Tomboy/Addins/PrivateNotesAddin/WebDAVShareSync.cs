using Tomboy.Sync;
using System;
using System.Collections.Generic;
using Mono.Unix;
using System.IO;

namespace Tomboy.PrivateNotes
{

	public class WebDAVShareSync
	{
		private Dictionary<String, WebDAVInterface> servers = null;
		private ShareProvider provider = null;
		private Dictionary<String, DirectoryInfo> shareBuffer = null;
		private String basePath = null;

		public WebDAVShareSync(ShareProvider shareProvider)
		{
			if ((shareProvider as WebDavShareProvider) == null)
				throw new Exception("shareProvider must be a WebDavShareProvider to be compatible with WebDAVShareSync");

			provider = shareProvider;
			servers = new Dictionary<string, WebDAVInterface>();
			shareBuffer = new Dictionary<string, DirectoryInfo>();
			basePath = Path.Combine(Services.NativeApplication.CacheDirectory, "sharedSync");
		}

		public void FetchAllShares()
		{
			List<NoteShare> shares = provider.GetShares();
			foreach (NoteShare share in shares)
			{
				if (servers.ContainsKey(share.shareTarget))
				{
					Logger.Info("already got connection object for server {0}", share.shareTarget);
				}
				else
				{
					Uri url = new Uri(share.shareTarget);
					string[] userInfo = url.UserInfo.Split(new char[] { ':' }, 2);
					String user = (userInfo.Length==2)?(userInfo[0]):("");
					String password = (userInfo.Length==2)?(userInfo[1]):("");
					String server = url.Scheme + "://" + url.Authority + ":" + url.Port;
					servers.Add(share.shareTarget, new WebDAVInterface(server, url.LocalPath, user, password, false));
				}
			}

			foreach (NoteShare share in shares)
			{
				WebDAVInterface dav = servers[share.shareTarget];
				if (dav.CheckForLockFile())
				{
					throw new Exception("TODO handle lockfile correctly!");
				}
				// TODO create/put lock file
				String randomName = System.Guid.NewGuid().ToString();
				String path = Path.Combine(basePath, randomName);
				Directory.CreateDirectory(path);
				shareBuffer.Add(share.noteId, new DirectoryInfo(path));
				dav.DownloadNotes(path);
			}

		}

		public void UploadNewOnes()
		{

		}

		public void CleanUp()
		{
			foreach(DirectoryInfo di in shareBuffer.Values) {
				if (di.Exists)
				{
					Util.DelelteFilesInDirectory(di.FullName);
					di.Delete(true);
				}
				else
				{
					Logger.Info("trying to clean up after share sync, but directory '{0}' didn't exist", di.Name);
				}
			}
			shareBuffer.Clear();

			// now purge the servers
			foreach (WebDAVInterface dav in servers.Values)
			{
				try
				{
					dav.RemoveLock();
				} catch {
					// ignored
				}
			}
			servers.Clear();
		}

	}

}