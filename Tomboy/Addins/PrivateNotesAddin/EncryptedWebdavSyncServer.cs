using System;
using System.Collections.Generic;
using System.Text;
using Tomboy.Sync;
using System.IO;

namespace Tomboy.PrivateNotes
{
	/// <summary>
	/// extends the EncryptedFileSystemSyncServer and implements a webdav based server-sync
	/// on top of the local encrypted sync service
	/// </summary>
	public class EncryptedWebdavSyncServer : EncryptedFileSystemSyncServer
	{
		/// <summary>
		/// the object that is used to communicate via webdav
		/// </summary>
		private WebDAVInterface webdavserver;

		private WebDAVShareSync shareSync;

		private Dictionary<String, DirectoryInfo> shareCopies = null;
		
		public EncryptedWebdavSyncServer(String _tempDir, byte[] _key, WebDAVInterface _webDav)
			: base(_tempDir, _key, _webDav)
		{

		}

		/// <summary>
		/// gets the path of the current revision dir
		/// </summary>
		/// <param name="rev"></param>
		/// <returns></returns>
		override internal string GetRevisionDirPath(int rev)
		{
			return serverPath;
		}

		/// <summary>
		/// called during the ctor of the base-class, here we fetch the data from the server to have the same 
		/// </summary>
		internal override void SetupWorkDirectory(object initParam)
		{
			Logger.Debug("basic stuff");
			base.SetupWorkDirectory(initParam);

			// because we only sync to the server and just copy the stuff everytime we
			// sync, we only need to use a temp-directory
			serverPath = cachePath;

			// refresh these variables because they would be wrong else
			lockPath = System.IO.Path.Combine(serverPath, "lock");
			manifestPath = System.IO.Path.Combine(serverPath, "manifest.xml");
			

			Logger.Debug("getting webdav interface");
			// this must be the webdav interface (not very nice way to do it, but did't know any other way)
			webdavserver = initParam as WebDAVInterface;

			Logger.Debug("deleting local files");
			// make our local sync dir empty
			Util.DelelteFilesInDirectory(serverPath);

			Logger.Debug("checking for remote lockfile");
			// fetch data from server
			if (webdavserver.CheckForLockFile())
			{
				Logger.Debug("downloading lockfile");
				webdavserver.DownloadLockFile(lockPath);
			}
			Logger.Debug("downloading notes");
			webdavserver.DownloadNotes(serverPath);

			GetFromShares();

			Logger.Debug("workdir setup done");
		}

		/// <summary>
		/// executed when the manifest file gets created
		/// </summary>
		/// <param name="pathToManifestFile"></param>
		internal override void OnManifestFileCreated(String pathToManifestFile) {
			// upload the manifest file:
			webdavserver.UploadFile(pathToManifestFile);
		}

		/// <summary>
		/// executed when a file should get deleted from the server
		/// </summary>
		/// <param name="pathToNote"></param>
		internal override void OnDeleteFile(String pathToNote)
		{
			// only use the name of the file (the rest is the local path which is irrelevant for the server)
			webdavserver.RemoveFile(System.IO.Path.GetFileName(pathToNote));
		}

		/// <summary>
		/// executed when a file should get updated to the server
		/// </summary>
		/// <param name="pathToNote"></param>
		internal override void OnUploadFile(String pathToNote) {
			if (pathToNote.EndsWith(".note")) {
				String id = GetNoteIdFromFileName(pathToNote);
				if (shareCopies.ContainsKey(id))
				{
					shareSync.UploadNewNote(id);
				}
			}

			webdavserver.UploadFile(pathToNote);
		}

		/// <summary>
		/// executed when lockfile gets deleted
		/// </summary>
		/// <param name="path"></param>
		internal override void RemoveLockFile(string path)
		{
			base.RemoveLockFile(path);
			// on server
			try
			{
				if (webdavserver.CheckForLockFile())
				{
					webdavserver.RemoveLock();
				}
			}
			catch (Exception e)
			{
				Logger.Warn("Error deleting servers lock: {1}", e.Message);
			}
		}

		private void GetFromShares()
		{
			ShareProvider provider = EncryptedWebdavSyncServiceAddin.shareProvider;
			shareSync = WebDAVShareSync.GetInstance(provider);

			shareSync.FetchAllShares();
		}

		public override bool CancelSyncTransaction() {
			bool success = base.CancelSyncTransaction();

			// clean up sync share
			if (shareSync != null)
			{
				shareSync.CleanUp();
			}
			return success;
		}

		public override int LatestRevision
		{
			get
			{
				int latestRev = -1;
				latestRev = GetRevisionFromManifestFile(manifestPath);

				// now check the shared notes
				shareCopies = shareSync.GetShareCopies();
				foreach (DirectoryInfo di in shareCopies.Values)
				{
					int revision = GetRevisionFromManifestFile(Path.Combine(di.FullName, "manifest.xml"));
					
					if (revision > latestRev)
						latestRev = revision;
				}

				return latestRev;
			}
		}

		internal override string GetNotePath(string defaultbasepath, string noteid)
		{
			if (shareCopies == null)
				throw new Exception("invalid state! you cannot call this until you have requested the shares from the share-provider!");
			if (shareCopies.ContainsKey(noteid))
			{
				// return shared note path
				return Path.Combine(shareCopies[noteid].FullName, noteid + ".note");
			}
			else
			{
				return base.GetNotePath(defaultbasepath, noteid);
			}
		}

		internal override Dictionary<String, int> GetNoteUpdatesIdsSince(int revision)
		{
			Dictionary<String, int> updates = base.GetNoteUpdatesIdsSince(revision);
			List<String> processedFiles = new List<string>();
			foreach (DirectoryInfo dir in shareCopies.Values)
			{
				String path = Path.Combine(dir.FullName, "manifest.xml");
				if (!processedFiles.Contains(path)) {
					// only processed if not already done
					processedFiles.Add(path);
					Dictionary<String, int> addme = GetNoteRevisionsFromManifest(path, revision);
					foreach (String key in addme.Keys)
					{
						updates.Add(key, addme[key]);
					}
				}
			}

			return updates;
		}

		internal override bool CreateManifestFile(String manifestFilePath, int newRevision, String serverid, Dictionary<String, int> notes)
		{
			// the key is the path of the manifest file
			// the value is a list of notes which have to be written to there
			Dictionary<String, List<String>> additionalManifests = new Dictionary<string, List<string>>();
			// check which of these notes are shared ones
			foreach (String note in notes.Keys)
			{
				if (shareCopies.ContainsKey(note)) {
					// a shared note, create a manifest file for it!
					DirectoryInfo di = shareCopies[note];
					String shareManifestPath = Path.Combine(di.FullName, "manifest.xml");
					if (!additionalManifests.ContainsKey(shareManifestPath))
					{
						additionalManifests.Add(shareManifestPath, new List<string>());
					}
					List<string> itsNotes = additionalManifests[shareManifestPath];
					itsNotes.Add(note);
				}
			}

			// now create the additional manifests:
			foreach (String shareManifest in additionalManifests.Keys)
			{
				Dictionary<String, int> theNotes = new Dictionary<string, int>();
				List<String> ids = additionalManifests[shareManifest];
				foreach (String id in ids) {
					theNotes.Add(id, notes[id]);
				}
				// TODO FIXME FATAL this doesn't work, because base.CreateManifestFile puts ALL the updated notes in!!!!
				// THIS SHOULD BE FIXED NOW!
				base.CreateManifestFile(shareManifest, newRevision, serverid, theNotes);
			}

			// XXX: currently we only return the status of the "normal" manifest.. maybe that's bad :S
			// now the "normal" thing
			return base.CreateManifestFile(manifestFilePath, newRevision, serverid, notes);
		}

	}
}
