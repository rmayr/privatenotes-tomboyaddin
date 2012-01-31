// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
using Tomboy.Sync;
using System;
using System.Collections.Generic;
using Mono.Unix;
using System.IO;
using System.Xml;
using Tomboy.PrivateNotes.Adress;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// an interface which implementations provide the connection to the actual share storages
	/// such as webdav or filesystem folder etc
	/// these objects should be used during the sync process to communicate with the shared storages
	/// </summary>
	public interface ShareSync
	{

		/// <summary>
		/// this should download all shares from the server(s)
		/// </summary>
		void FetchAllShares();

		/// <summary>
		/// returns a dictionary of all shares, the key is the note id
		/// the value a directoryinfo object of where the note is stored
		/// </summary>
		/// <returns></returns>
		Dictionary<String, DirectoryInfo> GetShareCopies();

		/// <summary>
		/// upload a new note which was shared
		/// </summary>
		/// <param name="noteId"></param>
		void UploadNewNote(String noteId);

		/// <summary>
		/// import a new note (add a new share from somebody else), identified by this url
		/// for a webdav share for example, this should include the full path with username and password.
		/// </summary>
		/// <param name="address"></param>
		void Import(String address);

		/// <summary>
		/// encrypts a note (the local copy) for uploading to the share (for the people
		/// with whom it is shared with)
		/// </summary>
		/// <param name="noteId">the recipients of this note are taken</param>
		/// <param name="fromFile">data from this file is encrypted</param>
		/// <param name="toFile">data is written to this file</param>
		void EncryptForShare(String noteId, String fromFile, String toFile);

		/// <summary>
		/// same as other version, just that data is read from data array
		/// </summary>
		/// <param name="noteId"></param>
		/// <param name="data"></param>
		/// <param name="toFile"></param>
		void EncryptForShare(String noteId, byte[] data, String toFile);

		/// <summary>
		/// gets a list of people with whom this file (note) is shared
		/// this ONLY works when there is an appropriate manifest-file in the
		/// same directory!
		/// </summary>
		/// <param name="fromFile"></param>
		/// <param name="sharedwith"></param>
		/// <returns>true if operation was successful</returns>
		bool GetSharePartners(String fromFile, out List<String> sharedwith);

		/// <summary>
		/// cleaning up at the end
		/// </summary>
		void CleanUp();
	}

	public class WebDAVShareSync : ShareSync
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

		/// <summary>
		/// <inheritdoc />
		/// </summary>
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

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public void Import(String path)
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

		/// <summary>
		/// <inheritdoc />
		/// </summary>
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

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public void EncryptForShare(String noteId, String fromFile, String toFile)
		{
			if (shareObjects.ContainsKey(noteId))
			{
				NoteShare share = shareObjects[noteId];
				SecurityWrapper.CopyAndEncryptShared(fromFile, toFile, new byte[0], share.sharedWith);
			}
			else
			{
				Logger.Warn("requested to encrypt note {0} which isn't shared!", noteId);
				throw new Exception(String.Format("requested to encrypt note {0} which isn't shared!", noteId));
			}
		}

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public void EncryptForShare(String noteId, byte[] data, String toFile)
		{
			if (shareObjects.ContainsKey(noteId))
			{
				NoteShare share = shareObjects[noteId];
				SecurityWrapper.SaveAsSharedEncryptedFile(toFile, data, new byte[0], share.sharedWith);
			}
			else
			{
				Logger.Warn("requested to encrypt note {0} which isn't shared!", noteId);
				throw new Exception(String.Format("requested to encrypt note {0} which isn't shared!", noteId));
			}
		}

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public bool GetSharePartners(String fromFile, out List<String> sharedwith)
		{
			bool ok = false;
			String dir = Path.GetDirectoryName(fromFile);
			String manifestPath = Path.Combine(dir, "manifest.xml");
			byte[] data = SecurityWrapper.DecryptFromSharedFile(manifestPath, out ok);
			if (!ok)
				throw new Exception("ENCRYPTION ERROR!");

			List<String> remoteSharers = new List<string>();
			using (Stream plainStream = new MemoryStream(data))
			{	
				XmlDocument doc = new XmlDocument();
				doc.Load(plainStream);
				XmlNodeList nodes = doc.SelectNodes("//with");
				foreach (XmlNode n in nodes)
				{
					String sharePartner = n.Attributes["partner"].Value;
					remoteSharers.Add(sharePartner);
				}
			}

			// now lookup the people by fingerprint:
			List<AddressBookEntry> entries = AddressBookFactory.Instance().GetDefault().GetEntries();
			Dictionary<String, String> fingerPrintToIdMapper = new Dictionary<string, string>();
			foreach (AddressBookEntry entry in entries)
			{
				string[] elements = entry.id.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
				String fprint = elements[1].Trim();
				if (!fingerPrintToIdMapper.ContainsKey(fprint))
					fingerPrintToIdMapper.Add(fprint, entry.id);
			}

			sharedwith = new List<string>();
			foreach (String sharer in remoteSharers)
			{
				if (!fingerPrintToIdMapper.ContainsKey(sharer))
				{
					throw new Exception(String.Format("can't import this share, because we don't have a key we need, fingerprint {0}", sharer));
				}
				sharedwith.Add(fingerPrintToIdMapper[sharer]);
			}

			return ok;
		}

		/// <summary>
		/// <inheritdoc />
		/// </summary>
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

		/// <summary>
		/// parses all the relevant parts of a webdav link from a URL
		/// </summary>
		/// <param name="link"></param>
		/// <param name="user"></param>
		/// <param name="password"></param>
		/// <param name="server"></param>
		/// <param name="basePath"></param>
		/// <returns></returns>
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