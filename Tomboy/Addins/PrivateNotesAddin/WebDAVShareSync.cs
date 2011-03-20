using Tomboy.Sync;
using System;
using System.Collections.Generic;
using Mono.Unix;
using System.IO;

namespace Tomboy.PrivateNotes
{

	public class WebDAVShareSync
	{
		/// <summary>
		/// servers by share.shareTarget (the webdav-url)
		/// </summary>
		private Dictionary<String, WebDAVInterface> servers = null;
		private ShareProvider provider = null;
		/// <summary>
		/// store-paths (directories) by note id
		/// </summary>
		private Dictionary<String, DirectoryInfo> shareBuffer = null;
		/// <summary>
		/// share objects by note id
		/// </summary>
		private Dictionary<String, NoteShare> shareObjects = null;
		private String basePath = null;
		private static WebDAVShareSync INSTANCE = null;

		public static WebDAVShareSync GetInstance(ShareProvider shareProvider)
		{
			if (INSTANCE == null)
			{
				INSTANCE = new WebDAVShareSync(shareProvider);
			}
			else
			{
				INSTANCE.CleanUp();
				INSTANCE.Init(shareProvider);
			}

			return INSTANCE;
		}

		private WebDAVShareSync(ShareProvider shareProvider)
		{
			if ((shareProvider as WebDavShareProvider) == null)
				throw new Exception("shareProvider must be a WebDavShareProvider to be compatible with WebDAVShareSync");
			Init(shareProvider);
		}

		private void Init(ShareProvider shareProvider)
		{
			provider = shareProvider;
			servers = new Dictionary<string, WebDAVInterface>();
			shareBuffer = new Dictionary<string, DirectoryInfo>();
			basePath = Path.Combine(Services.NativeApplication.CacheDirectory, "sharedSync");
			shareObjects = new Dictionary<string, NoteShare>();
		}

		public void FetchAllShares()
		{
			CleanUp();

			// always get shares fresh from the shareProvider
			List<NoteShare> shares = provider.GetShares();

			foreach (NoteShare share in shares)
			{
				// add to our internal store
				shareObjects.Add(share.noteId, share);

				if (servers.ContainsKey(share.shareTarget))
				{
					Logger.Info("already got connection object for server {0}", share.shareTarget);
				}
				else
				{
					//Uri url = new Uri(share.shareTarget);
					//string[] userInfo = url.UserInfo.Split(new char[] { ':' }, 2);
					//String user = (userInfo.Length==2)?(userInfo[0]):("");
					//String password = (userInfo.Length==2)?(userInfo[1]):("");
					//String server = url.Scheme + "://" + url.Authority + ":" + url.Port;
					String user, password, server, serverbasepath;
					ParseFromLink(share.shareTarget, out user, out password, out server, out serverbasepath);
					servers.Add(share.shareTarget, new WebDAVInterface(server, serverbasepath, user, password, false));
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

		public void ImportFromWebdav(String path)
		{
			String user, pw, server, serverbase;
			ParseFromLink(path, out user, out pw, out server, out serverbase);
			WebDAVInterface wdi = new WebDAVInterface(server, serverbase, user, pw, false);

			if (wdi.CheckForLockFile())
			{
				throw new Exception("TODO handle lockfile correctly!");
			}
			// TODO create/put lock file
			String randomName = System.Guid.NewGuid().ToString();
			String localpath = Path.Combine(basePath, "import_" + randomName);
			Directory.CreateDirectory(localpath);
			wdi.DownloadNotes(localpath);
			DirectoryInfo di = new DirectoryInfo(localpath);
			FileInfo[] files = di.GetFiles("*.note");
			foreach (FileInfo fi in files) {
				shareBuffer.Add(fi.Name.Replace(".note", ""), di);
			}

		}

		public Dictionary<String, DirectoryInfo> GetShareCopies()
		{
			return shareBuffer;
		}

		/// <summary>
		/// manifest files are automatically uploaded as well
		/// </summary>
		/// <param name="notes"></param>
		public void UploadNewNote(String noteId)
		{
			if (shareBuffer.ContainsKey(noteId) && shareObjects.ContainsKey(noteId))
			{
				NoteShare shareObj = shareObjects[noteId];
				WebDAVInterface dav = servers[shareObj.shareTarget];
				String noteFilePath = Path.Combine(shareBuffer[noteId].FullName, noteId + ".note");
				dav.UploadFile(noteFilePath);
				// TODO FIXME don't upload the manifest file multiple times!!!
				dav.UploadFile(Path.Combine(shareBuffer[noteId].FullName, "manifest.xml"));
			}
			else
				Logger.Warn("Note {0} is not part of the shared notes!", noteId);
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
					// TODO how to know if it is still "our" lock, not from another client syncing!
					//dav.RemoveLock();
				} catch {
					// ignored
				}
			}
			servers.Clear();

			shareObjects.Clear();
		}

		public static bool ParseFromLink(String link, out String user, out String password, out String server, out String basePath) {
			Uri url = new Uri(link);
			string[] userInfo = url.UserInfo.Split(new char[] { ':' }, 2);
			user = (userInfo.Length==2)?(userInfo[0]):("");
			password = (userInfo.Length==2)?(userInfo[1]):("");
			server = url.Scheme + "://" + url.Authority + ":" + url.Port;
			basePath = url.LocalPath;
			return true;
		}

	}

}