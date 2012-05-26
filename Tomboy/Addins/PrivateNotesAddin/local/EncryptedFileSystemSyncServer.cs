// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
using System;
using System.Collections.Generic;
using System.Text;
using Tomboy;
using Mono.Unix;
using System.IO;
using System.Xml;
using Tomboy.PrivateNotes.Crypto;
using Tomboy.PrivateNotes;

namespace Tomboy.Sync
{
		public class EncryptedFileSystemSyncServer : SyncServer
		{
			public static ShareProvider shareProvider = null;

			private byte[] myKey;
			// for synchronization with shared storages
			internal ShareSync shareSync;

			/// <summary>
			/// contains note-id and the directory where this shares objects are handled
			/// </summary>
			internal Dictionary<String, DirectoryInfo> shareCopies = null;
			internal Dictionary<String, int> updatedShareRevisions = null;

			private List<string> updatedNotes;
			private List<string> deletedNotes;

			private string serverId;

			internal string serverPath;
			internal string cachePath;
			internal string lockPath;
			internal string manifestPath;

			private int newRevision;
			private string newRevisionPath;

			private static DateTime initialSyncAttempt = DateTime.MinValue;
			private static string lastSyncLockHash = string.Empty;
			InterruptableTimeout lockTimeout;
			SyncLockInfo syncLock;

			public EncryptedFileSystemSyncServer(string localSyncPath, byte[] _password, object _initParam)
			{
				shareProvider = SecureSharingFactory.Get().GetShareProvider();
				//shareProvider = new WebDavShareProvider();

				myKey = _password;
				serverPath = localSyncPath;

				cachePath = Services.NativeApplication.CacheDirectory;
				cachePath = Path.Combine(cachePath, "sync_temp");
				lockPath = Path.Combine(serverPath, "lock");
				manifestPath = Path.Combine(serverPath, "manifest.xml");

				// setup sync-temp-folder
				if (!Directory.Exists(cachePath))
				{
					Directory.CreateDirectory(cachePath);
				}
				else
				{
					// Empty the temp dir
					Logger.Debug("purging directory " + cachePath);
					foreach (string oldFile in Directory.GetFiles(cachePath))
					{
						try
						{
							File.Delete(oldFile);
						}
						catch { }
					}
					Logger.Debug("purging done");
				}

				Logger.Debug("setupWorkDir");
				SetupWorkDirectory(_initParam);
				Logger.Debug("setupWorkDir done");

				Logger.Debug("getRevision");
				newRevision = LatestRevision + 1;
				newRevisionPath = GetRevisionDirPath(newRevision);
				Logger.Debug("getRevision done");

				Logger.Debug("final stuff");
				lockTimeout = new InterruptableTimeout();
				lockTimeout.Timeout += LockTimeout;
				syncLock = new SyncLockInfo();
				Logger.Debug("final stuff done");
			}

			virtual internal void SetupWorkDirectory(object initParam)
			{
				// nothing to do in local version
				GetFromShares();
			}

			/// <summary>
			/// downloads all notes from the shares
			/// </summary>
			private void GetFromShares()
			{
				updatedShareRevisions = new Dictionary<string, int>();
				shareSync = SecureSharingFactory.Get().GetShareSync();
				shareCopies = shareSync.GetShareCopies();

				shareSync.FetchAllShares();
			}

			virtual internal void OnManifestFileCreated(String pathToManifestFile) {
				// nothing to do in local version
			}

			virtual internal void OnDeleteFile(String pathToNote) {
				// nothing to do in local version
			}

			virtual internal bool OnUploadFile(String pathToNote) {
				// if its a shared note, foreward this call to the shareSync

				if (pathToNote.EndsWith(".note"))
				{
					String id = Util.GetNoteIdFromFileName(pathToNote);
					if (shareCopies.ContainsKey(id))
					{
						// TODO FIXME SEVERE: this is juts a hack, because if we didn't get the note from the server, 
						// it isn't in the correct directory yet! check how it works for the normal sync and to it like this for shared
						// notes also!
						File.Copy(Path.Combine(cachePath, id + ".note"), Path.Combine(shareCopies[id].FullName, id + ".note"), true);
						shareSync.UploadNewNote(id);
						return true;
					}
				}
				return false;
			}

			public virtual void UploadNotes(IList<Note> notes)
			{
				if (Directory.Exists(newRevisionPath) == false)
				{
					DirectoryInfo info = Directory.CreateDirectory(newRevisionPath);
					AdjustPermissions(info.Parent.FullName);
					AdjustPermissions(newRevisionPath);
				}
				Logger.Debug("UploadNotes: notes.Count = {0}", notes.Count);
				foreach (Note note in notes)
				{
					try
					{
						if (!File.Exists(note.FilePath))
						{
							// fix strange tomboy sync bug, by forcing save here
							/*GuiUtils.GtkInvokeAndWait(() =>
							{
								note.QueueSave(ChangeType.NoChange);
								note.Save();
							});*/
							if (!File.Exists(note.FilePath))
							{
								Logger.Warn("note is refusing to get saved... bad note... bad");
								Logger.Error("Will not upload note " + note.Id + " because of disobedience");
								continue;
							}
						}

						string serverNotePath = Path.Combine(newRevisionPath, Path.GetFileName(note.FilePath));
						if (shareCopies.ContainsKey(note.Id))
						{
							shareSync.EncryptForShare(note.Id, note.FilePath, serverNotePath);
							// TODO: is this necessary? should it be stored only in the folder where we move it now?
							// .... figure it out
							File.Copy(serverNotePath, Path.Combine(shareCopies[note.Id].FullName, new FileInfo(serverNotePath).Name), true);
						}
						else
						{
							SecurityWrapper.CopyAndEncrypt(note.FilePath, serverNotePath, myKey);
						}

						//File.Copy(note.FilePath, serverNotePath, true);

						// upload to webdav takes place in commit-function
						AdjustPermissions(serverNotePath);
						updatedNotes.Add(Path.GetFileNameWithoutExtension(note.FilePath));
					}
					catch (Exception e)
					{
						Logger.Error("Sync: Error uploading note \"{0}\": {1}", note.Title, e.Message);
						throw;
					}
				}
			}

			public virtual void DeleteNotes(IList<string> deletedNoteUUIDs)
			{
				foreach (string uuid in deletedNoteUUIDs)
				{
					try
					{
						deletedNotes.Add(uuid);

						// delete from server happens in the commit-function
					}
					catch (Exception e)
					{
						Logger.Error("Sync: Error deleting note: " + e.Message);
					}
				}
			}

			public IList<string> GetAllNoteUUIDs()
			{
				List<string> noteUUIDs = new List<string>();

				Dictionary<string, int> updates = GetNoteUpdatesIdsSince(-1);
				noteUUIDs.AddRange(updates.Keys);
				return noteUUIDs;
			}

			public bool UpdatesAvailableSince(int revision)
			{
				return LatestRevision > revision; // TODO: Mounting, etc?
			}

			public virtual IDictionary<string, NoteUpdate> GetNoteUpdatesSince(int revision)
			{
				Dictionary<string, NoteUpdate> noteUpdates = new Dictionary<string, NoteUpdate>();
				Dictionary<string, int> updates = new Dictionary<string, int>();
				updates = GetNoteUpdatesIdsSince(revision);

				// now decrypt and construct NoteUpdate objects
				foreach (string id in updates.Keys)
				{
					int rev = updates[id];
					if (noteUpdates.ContainsKey(id) == false)
					{
						// Copy the file from the server to the temp directory
						string revDir = GetRevisionDirPath(rev);
						string serverNotePath = GetNotePath(revDir, id);
						if (File.Exists(serverNotePath))
						{
							//string noteTempPath = Path.Combine(tempPath, id + ".note");
							// DON'T ENCRYPT HERE because we are getting the already encrypted file from the server
							//SecurityWrapper.CopyAndEncrypt(serverNotePath, noteTempPath, myKey);
							//File.Copy(serverNotePath, noteTempPath, true);

							// Get the title, contents, etc.
							string noteTitle = string.Empty;
							string noteXml = null;

							{
								// decrypt the note:
								bool ok;
								byte[] contents = null;

								if (shareCopies.ContainsKey(id))
								{
									// use shared decrypt
									contents = SecurityWrapper.DecryptFromSharedFile(serverNotePath, out ok);
									// since it's shared it uses its own revision number, but this should not be forewarded to Tomboy:
									rev = revision + 1;
								}
								else
								{
									// normal decrypt
									using (FileStream fin = File.Open(serverNotePath, FileMode.Open))
									{
										contents = SecurityWrapper.DecryptFromFile(serverNotePath, fin, myKey, out ok);
									}
								}
								noteXml = Util.FromBytes(contents);

								// solve nasty BOM problem -__-
								int index = noteXml.IndexOf('<');
								if (index > 0)
								{
									noteXml = noteXml.Substring(index, noteXml.Length - index);
								}

							}
							NoteUpdate update = new NoteUpdate(noteXml, noteTitle, id, rev);
							noteUpdates[id] = update;
						}
						else
						{
							Logger.Info("File {0} doesn't exist, skipping", serverNotePath);
						}
					}
				}

				Logger.Debug("GetNoteUpdatesSince ({0}) returning: {1}", revision, noteUpdates.Count);
				return noteUpdates;
			}

			public virtual bool BeginSyncTransaction()
			{
				// Lock expiration: If a lock file exists on the server, a client
				// will never be able to synchronize on its first attempt.	The
				// client should record the time elapsed
				if (File.Exists(lockPath))
				{
					SyncLockInfo currentSyncLock = CurrentSyncLock;
					if (initialSyncAttempt == DateTime.MinValue)
					{
						Logger.Debug("Sync: Discovered a sync lock file, wait at least {0} before trying again.", currentSyncLock.Duration);
						// This is our initial attempt to sync and we've detected
						// a sync file, so we're gonna have to wait.
						initialSyncAttempt = DateTime.Now;
						lastSyncLockHash = currentSyncLock.HashString;
						return false;
					}
					else if (lastSyncLockHash != currentSyncLock.HashString)
					{
						Logger.Debug("Sync: Updated sync lock file discovered, wait at least {0} before trying again.", currentSyncLock.Duration);
						// The sync lock has been updated and is still a valid lock
						initialSyncAttempt = DateTime.Now;
						lastSyncLockHash = currentSyncLock.HashString;
						return false;
					}
					else
					{
						if (lastSyncLockHash == currentSyncLock.HashString)
						{
							// The sync lock has is the same so check to see if the
							// duration of the lock has expired.	If it hasn't, wait
							// even longer.
							if (DateTime.Now - currentSyncLock.Duration < initialSyncAttempt)
							{
								Logger.Debug("Sync: You haven't waited long enough for the sync file to expire.");
								return false;
							}
						}

						// Cleanup Old Sync Lock!
						CleanupOldSync(currentSyncLock);
					}
				}

				// Reset the initialSyncAttempt
				initialSyncAttempt = DateTime.MinValue;
				lastSyncLockHash = string.Empty;

				// Create a new lock file so other clients know another client is
				// actively synchronizing right now.
				syncLock.RenewCount = 0;
				syncLock.Revision = newRevision;
				UpdateLockFile(syncLock);
				// TODO: Verify that the lockTimeout is actually working or figure
				// out some other way to automatically update the lock file.
				// Reset the timer to 20 seconds sooner than the sync lock duration
				lockTimeout.Reset((uint)syncLock.Duration.TotalMilliseconds - 20000);

				updatedNotes = new List<string>();
				deletedNotes = new List<string>();

				return true;
			}

			public virtual bool CommitSyncTransaction()
			{
				bool commitSucceeded = false;

				if (updatedNotes.Count > 0 || deletedNotes.Count > 0)
				{
					// TODO: better error-checking
					string manifestFilePath = Path.Combine(newRevisionPath, "manifest.xml");
					if (!Directory.Exists(newRevisionPath))
					{
						DirectoryInfo info = Directory.CreateDirectory(newRevisionPath);
						AdjustPermissions(info.Parent.FullName);
						AdjustPermissions(newRevisionPath);
					}

					XmlNodeList noteNodes = null;
					if (IsValidXmlFile(manifestPath) == true)
					{
						using (FileStream fs = new FileStream(manifestPath, FileMode.Open))
						{
							bool ok;
							Stream plainStream = SecurityWrapper.DecryptFromStream(manifestPath, fs, myKey, out ok);
							if (!ok)
								throw new Exception("ENCRYPTION ERROR!");

							XmlDocument doc = new XmlDocument();
							doc.Load(plainStream);
							noteNodes = doc.SelectNodes("//note");
						}
					}
					else
					{
						using (StringReader sr = new StringReader("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<sync>\n</sync>"))
						{
							XmlDocument doc = new XmlDocument();
							doc.Load(sr);
							noteNodes = doc.SelectNodes("//note");
						}
					}

#region createManifestFile
					// Write out the new manifest file
					Dictionary<String, int> allNotes = new Dictionary<string, int>();
					//List<NoteWithRev> allNotes = new List<NoteWithRev>();
					foreach (XmlNode node in noteNodes)
					{
						string id = node.SelectSingleNode("@id").InnerText;
						string rev = node.SelectSingleNode("@rev").InnerText;
						allNotes.Add(id, Int32.Parse(rev));
					}
					// also, add all updated notes:
					foreach (String id in updatedNotes)
					{
						int revision = newRevision;
						if (shareCopies.ContainsKey(id))
						{
							// shared one, use that updated
							int currentServerRev = -1;
							updatedShareRevisions.TryGetValue(id, out currentServerRev);
							// get the highest current version
							currentServerRev = Math.Max(currentServerRev, shareProvider.GetNoteShare(id).revision);
							// because we changed the note locally, the new version is even 1 higher
							revision = currentServerRev + 1;
							Util.PutInDict(updatedShareRevisions, id, revision);
						}
						// overwrite if already in there, else add
						Util.PutInDict(allNotes, id, revision);
					}

					bool manifestCreated = CreateManifestFile(manifestFilePath, newRevision, serverId, allNotes);
					if (!manifestCreated)
						throw new Exception("could not create manifest file, cannot recover from this error.");

					AdjustPermissions(manifestFilePath);
#endregion

					// only use this if we use the revision-folder-mode
					if (!manifestFilePath.Equals(manifestPath))
					{
						throw new Exception("unsupported sync type!");
					}
					else
					{
						Logger.Info("probably doing sync without revision-hierarchy");

						OnManifestFileCreated(manifestFilePath);

						// this is just a simple cleanup because we only use one directory
						// delete the notes that were deleted in this one directory:
						try
						{
							FileInfo manifestFilePathInfo = new FileInfo(manifestFilePath);
							List<FileInfo> allNoteFiles = GetAllNoteFiles(manifestFilePathInfo.Directory);
							foreach (FileInfo file in allNoteFiles)
							{
								string fileGuid = Path.GetFileNameWithoutExtension(file.Name);
								if (deletedNotes.Contains(fileGuid))
								{
									File.Delete(file.FullName);
									OnDeleteFile(file.FullName);
									if (shareCopies.ContainsKey(fileGuid))
									{
										shareProvider.RemoveShare(fileGuid);
									}
								}

								if (updatedNotes.Contains(fileGuid))
								{
									Logger.Info("uploading " + fileGuid);
									OnUploadFile(file.FullName);
								}
							}
							commitSucceeded = true;
						}
						catch (Exception e)
						{
							Logger.Error("Exception during committing to server. " +
														"Some files may have not been brought up to the latest version." + 
														"Here's the error:\n" +
														e.Message);
						}

					}
				}
				else
				{
					// no changes (no updates/deletes)
				    commitSucceeded = true;
				}

				lockTimeout.Cancel();
				RemoveLockFile(lockPath);
				// update local share sync revs:
				foreach (KeyValuePair<string, int> shareUpd in updatedShareRevisions)
				{
					NoteShare shareInfoObj = shareProvider.GetNoteShare(shareUpd.Key);
					if (shareInfoObj != null)
					{
						shareInfoObj.revision = shareUpd.Value;
					}
				}
				shareProvider.SaveShares();

				Statistics.Instance.SetSyncStatus(commitSucceeded);
				return commitSucceeded;
			}

			// FIXME: Return false if this is a bad time to cancel sync?
			public virtual bool CancelSyncTransaction()
			{
				lockTimeout.Cancel();
				RemoveLockFile(lockPath);
				
				// clean up sync share
				if (shareSync != null)
				{
					shareSync.CleanUp();
				}

				Statistics.Instance.SetSyncStatus(false);
				return true;
			}

			public virtual int LatestRevision
			{
				get
				{
					int latestRev = -1;
					int latestRevDir = -1;
					latestRev = GetRevisionFromManifestFile(false, manifestPath);

					return latestRev;
				}
			}

			public virtual SyncLockInfo CurrentSyncLock
			{
				get
				{
					SyncLockInfo syncLockInfo = new SyncLockInfo();

					if (IsValidXmlFile(lockPath))
					{
						// TODO: Permissions errors
						using (FileStream fs = new FileStream(lockPath, FileMode.Open))
						{
							XmlDocument doc = new XmlDocument();
							// TODO: Handle invalid XML
							doc.Load(fs);

							XmlNode node = doc.SelectSingleNode("//transaction-id/text ()");
							if (node != null)
							{
								string transaction_id_txt = node.InnerText;
								syncLockInfo.TransactionId = transaction_id_txt;
							}

							node = doc.SelectSingleNode("//client-id/text ()");
							if (node != null)
							{
								string client_id_txt = node.InnerText;
								syncLockInfo.ClientId = client_id_txt;
							}

							node = doc.SelectSingleNode("//renew-count/text ()");
							if (node != null)
							{
								string renew_txt = node.InnerText;
								syncLockInfo.RenewCount = Int32.Parse(renew_txt);
							}

							node = doc.SelectSingleNode("//lock-expiration-duration/text ()");
							if (node != null)
							{
								string span_txt = node.InnerText;
								syncLockInfo.Duration = TimeSpan.Parse(span_txt);
							}

							node = doc.SelectSingleNode("//revision/text ()");
							if (node != null)
							{
								string revision_txt = node.InnerText;
								syncLockInfo.Revision = Int32.Parse(revision_txt);
							}
						}
					}

					return syncLockInfo;
				}
			}

			public virtual string Id
			{
				get
				{
					serverId = null;

					// Attempt to read from manifest file first
					if (IsValidXmlFile(manifestPath))
					{
						using (FileStream fs = new FileStream(manifestPath, FileMode.Open))
						{
							bool ok;
							Stream plainStream = SecurityWrapper.DecryptFromStream(manifestPath, fs, myKey, out ok);
							if (!ok)
								throw new Exception("ENCRYPTION ERROR!");

							XmlDocument doc = new XmlDocument();
							doc.Load(plainStream);
							XmlNode syncNode = doc.SelectSingleNode("//sync");
							XmlNode serverIdNode = syncNode.Attributes.GetNamedItem("server-id");
							if (serverIdNode != null && !(string.IsNullOrEmpty(serverIdNode.InnerText)))
								serverId = serverIdNode.InnerText;
						}
					}

					// Generate a new ID if there isn't already one
					if (serverId == null)
						serverId = System.Guid.NewGuid().ToString();

					return serverId;
				}
			}

			#region Private/Internal Methods

			internal String GetNotePath(String defaultbasepath, String noteid) 
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
					return Path.Combine(defaultbasepath, noteid + ".note");
				}
			}

			/// <summary>
			/// Gets updated notes from private and shared locations
			/// </summary>
			/// <param name="revision"></param>
			/// <returns></returns>
			virtual internal Dictionary<String, int> GetNoteUpdatesIdsSince(int revision)
			{
				// private ones
				Dictionary<String, int> updates = GetNoteRevisionsFromManifest(false, manifestPath, revision);
				List<String> processedFiles = new List<string>();

				// now process all shared manifests
				foreach (KeyValuePair<String, DirectoryInfo> shareEntry in shareCopies)
				{
					DirectoryInfo dir = shareEntry.Value;
					String path = Path.Combine(dir.FullName, "manifest.xml");
					if (!processedFiles.Contains(path))
					{
						if (updates.ContainsKey(shareEntry.Key))
						{
							// somehow this is in the normal manifest, but since it's shared
							// it doesn't belong there, remove that info
							updates.Remove(shareEntry.Key);
						}
						// only processed if not already done
						processedFiles.Add(path);
						// use individual max-rev per share
						// but if -1 is used (get all note ids) we use that!
						int shareRevision = revision == -1 ? -1 : shareProvider.GetNoteShare(shareEntry.Key).revision;
						Dictionary<String, int> addme = GetNoteRevisionsFromManifest(true, path, shareRevision);
						foreach (String key in addme.Keys)
						{
						    int updatedRev = addme[key];
							if (!shareCopies.ContainsKey(key))
							{
								// o_O this is a shared note we totally didn't know about?! it was somehow
								// uploaded into that dir + added to the manifest
								// not sure yet how to handle this...
								Logger.Warn("new shared note was discovered! don't know how to handle this!" +
								"Automatic adding if notes is not yet supported, because i think it's problematic during sync... maybe?");
							}
							else
							{
								// it's ok, it's a shared note we knew about
								if (updates.ContainsKey(key))
								{
									if (updates[key] < updatedRev) // overwrite if the one from the shared folder is newer
										updates[key] = updatedRev;
								}
								else
								{
									updates.Add(key, updatedRev);
								}
								// save new share revision for saving on commit
								if (updatedRev > shareRevision)
								{
									Util.PutInDict(updatedShareRevisions, key, updatedRev);
								}
							}
						}
					}
				}

				return updates;
			}

			/// <summary>
			/// Gets all notes from the manifest that have a revision bigger than the supplied revision
			/// </summary>
			/// <param name="shared">true for shared manifests</param>
			/// <param name="filePath">manifest file path</param>
			/// <param name="revision">only notes with bigger rev than this are returned</param>
			/// <returns></returns>
			virtual internal Dictionary<String, int> GetNoteRevisionsFromManifest(bool shared, String filePath, int revision)
			{
				Dictionary<String, int> updates = new Dictionary<String, int>();
				if (!File.Exists(filePath))
				{
					Logger.Warn("file {0} doesn't exist (GetNoteRevisionsFromManifest)", filePath);
					return updates;
				}
				
				// disabled because of shared& normal encryption // if (IsValidXmlFile(filePath))
				{
					Stream plainStream = null;
					bool ok = false;
					if (!shared) {
						using (FileStream fs = new FileStream(filePath, FileMode.Open))
						{
							plainStream = SecurityWrapper.DecryptFromStream(filePath, fs, myKey, out ok);
						}
					} else {
						plainStream = new MemoryStream(SecurityWrapper.DecryptFromSharedFile(filePath, out ok));
					}
					if (!ok)
						throw new	Exception("ENCRYPTION ERROR!");

					XmlDocument doc = new XmlDocument();
					doc.Load(plainStream);

					string xpath =
									string.Format("//note[@rev > {0}]", revision.ToString());
					XmlNodeList noteNodes = doc.SelectNodes(xpath);
					Logger.Debug("GetNoteUpdatesSince xpath returned {0} nodes", noteNodes.Count);

					foreach (XmlNode node in noteNodes)
					{
						string id = node.SelectSingleNode("@id").InnerText;
						int rev = Int32.Parse(node.SelectSingleNode("@rev").InnerText);
						updates.Add(id, rev);
					}
				}
				return updates;
			}

			internal Dictionary<String, int> GetNoteRevisionsFromManifest(String filePath)
			{
				Dictionary<String, int> noterevisions = new Dictionary<String, int>();
				if (IsValidXmlFile(filePath))
				{
					using (FileStream fs = new FileStream(filePath, FileMode.Open))
					{
						Stream plainStream;
						{
							bool ok;
							plainStream = SecurityWrapper.DecryptFromStream(filePath, fs, myKey, out ok);
							if (!ok)
								throw new Exception("ENCRYPTION ERROR!");
						}

						XmlDocument doc = new XmlDocument();
						doc.Load(plainStream);

						string xpath = "//note";
						XmlNodeList noteNodes = doc.SelectNodes(xpath);
						Logger.Debug("GetNoteUpdatesSince xpath returned {0} nodes", noteNodes.Count);

						foreach (XmlNode node in noteNodes)
						{
							string id = node.SelectSingleNode("@id").InnerText;
							int rev = Int32.Parse(node.SelectSingleNode("@rev").InnerText);
							noterevisions.Add(id, rev);
						}

					}
				}
				return noterevisions;
			}
			
			// NOTE: Assumes serverPath is set
			virtual internal string GetRevisionDirPath(int rev)
			{
#if !TRYDIRS
				return serverPath;
#else
				return Path.Combine(
				               Path.Combine(serverPath, (rev / 100).ToString()),
				               rev.ToString());
#endif	
			}

			private void UpdateLockFile(SyncLockInfo syncLockInfo)
			{
				XmlWriter xml = XmlWriter.Create(lockPath, XmlEncoder.DocumentSettings);
				try
				{
					xml.WriteStartDocument();
					xml.WriteStartElement(null, "lock", null);

					xml.WriteStartElement(null, "transaction-id", null);
					xml.WriteString(syncLockInfo.TransactionId);
					xml.WriteEndElement();

					xml.WriteStartElement(null, "client-id", null);
					xml.WriteString(syncLockInfo.ClientId);
					xml.WriteEndElement();

					xml.WriteStartElement(null, "renew-count", null);
					xml.WriteString(string.Format("{0}", syncLockInfo.RenewCount));
					xml.WriteEndElement();

					xml.WriteStartElement(null, "lock-expiration-duration", null);
					xml.WriteString(syncLockInfo.Duration.ToString());
					xml.WriteEndElement();

					xml.WriteStartElement(null, "revision", null);
					xml.WriteString(syncLockInfo.Revision.ToString());
					xml.WriteEndElement();

					xml.WriteEndElement();
					xml.WriteEndDocument();
				}
				finally
				{
					xml.Close();
				}

				AdjustPermissions(lockPath);
				// TODO UPLOAD TO WEBDAV HERE
			}

			/// <summary>
			/// This method is used when the sync lock file is determined to be out
			/// of date.	It will check to see if the manifest.xml file exists and
			/// check whether it is valid (must be a valid XML file).
			/// </summary>
			private void CleanupOldSync(SyncLockInfo syncLockInfo)
			{
				Logger.Debug("Sync: Cleaning up a previous failed sync transaction");
				int rev = LatestRevision;
				if (rev >= 0 && !IsValidXmlFile(manifestPath))
				{
					// Time to discover the latest valid revision
					// If no manifest.xml file exists, that means we've got to
					// figure out if there are any previous revisions with valid
					// manifest.xml files around.
					for (; rev >= 0; rev--)
					{
						string revParentPath = GetRevisionDirPath(rev);
						string manPath = Path.Combine(revParentPath, "manifest.xml");

						if (IsValidXmlFile(manPath) == false)
							continue;

						// Restore a valid manifest path
						File.Copy(manPath, manifestPath, true);
						break;
					}
				}

				// Delete the old lock file
				Logger.Debug("Sync: Deleting expired lockfile");
				RemoveLockFile(lockPath);
			}

			/// <summary>
			/// Check that xmlFilePath points to an existing valid XML file.
			/// This is done by ensuring that an XmlDocument can be created from
			/// its contents.
			///</summary>
			internal bool IsValidXmlFile(string xmlFilePath)
			{
				// Check that file exists
				if (!File.Exists(xmlFilePath))
					return false;

				// TODO: Permissions errors
				// Attempt to load the file and parse it as XML
				try
				{
					using (FileStream fs = new FileStream(xmlFilePath, FileMode.Open))
					{
						bool ok;
						Stream plainStream = SecurityWrapper.DecryptFromStream(xmlFilePath, fs, myKey, out ok);
						if (!ok)
							throw new EncryptionException("ENCRYPTION ERROR!");

						XmlDocument doc = new XmlDocument();
						// TODO: Make this be a validating XML reader.	Not sure if it's validating yet.
						doc.Load(plainStream);
					}
				}
				catch (PasswordException)
				{
					throw;
				}
				catch (EncryptionException ee)
				{
					throw new Exception("there was a problem with the file encryption, can't sync!", ee);
				}
				catch (Exception e)
				{
					Logger.Debug("Exception while validating lock file: " + e.ToString());
					return false;
				}

				return true;
			}

			private void AdjustPermissions(string path)
			{
#if !WIN32
				Mono.Unix.Native.Syscall.chmod(path, Mono.Unix.Native.FilePermissions.ACCESSPERMS);
#endif
			}

			internal virtual void RemoveLockFile(string path)
			{
				try
				{
					File.Delete(path);
				}
				catch (Exception e)
				{
					Logger.Warn("Error deleting the lock \"{0}\": {1}", lockPath, e.Message);
				}
			}

			internal int GetRevisionFromManifestFile(bool shared, String path)
			{
				int latestRev = -1;
				if (!File.Exists(path))
					return latestRev;

				// disabled check because we would have to deal with 2 different kinds of encryptions//if (IsValidXmlFile(path) == true)
				{
					Stream plainStream = null;
					bool ok = true;
					if (!shared)
					{
						using (FileStream fs = new FileStream(path, FileMode.Open))
						{
							plainStream = SecurityWrapper.DecryptFromStream(path, fs, myKey, out ok);
						}
					}
					else
					{
						// shared
						plainStream = new MemoryStream(SecurityWrapper.DecryptFromSharedFile(path, out ok));
					}

					if (!ok)
						throw new Exception("ENCRYPTION ERROR!");

					XmlDocument doc = new XmlDocument();
					doc.Load(plainStream);
					XmlNode syncNode = doc.SelectSingleNode("//sync");
					string latestRevStr = syncNode.Attributes.GetNamedItem("revision").InnerText;
					if (latestRevStr != null && latestRevStr != string.Empty)
						latestRev = Int32.Parse(latestRevStr);
				}
				return latestRev;
			}
			
			internal virtual List<FileInfo> GetAllNoteFiles(DirectoryInfo defaultPath) {
				List<FileInfo> files = new List<FileInfo>();
				files.AddRange(defaultPath.GetFiles());
				return files;
			}
			
			public struct NoteWithRev {
				public NoteWithRev(String id, int rev)
				{
					this.noteId = id;
					this.rev = rev;
				}

				public String noteId;
				public int rev;
			}

			internal bool CreateManifestFile(String manifestFilePath, int newRevision, String serverid, Dictionary<String, int> notes)
			{
				Dictionary<String, int> privateNotes = new Dictionary<string, int>(notes);
				// the key is the path of the manifest file
				// the value is a list of notes which have to be written to there
				Dictionary<String, List<String>> additionalManifests = new Dictionary<string, List<string>>();
				// check which of these notes are shared ones
				foreach (String note in notes.Keys)
				{
					if (shareCopies.ContainsKey(note))
					{
						// a shared note, create a manifest file for it!
						DirectoryInfo di = shareCopies[note];
						String shareManifestPath = Path.Combine(di.FullName, "manifest.xml");
						if (!additionalManifests.ContainsKey(shareManifestPath))
						{
							additionalManifests.Add(shareManifestPath, new List<string>());
						}
						List<string> itsNotes = additionalManifests[shareManifestPath];
						itsNotes.Add(note);
						// since it's shared, it doesn't belong to the private notes manifest file
						privateNotes.Remove(note);
					}
				}

				// now create the additional manifests:
				foreach (String shareManifest in additionalManifests.Keys)
				{
					try
					{
						Dictionary<String, int> theNotes = new Dictionary<string, int>();
						List<String> ids = additionalManifests[shareManifest];
						int shareRevision = 0;
						foreach (String id in ids)
						{
							int noteRev = notes[id];
							shareRevision = Math.Max(shareRevision, noteRev);
							theNotes.Add(id, notes[id]);
						}
						// TODO FIXME FATAL this doesn't work, because base.CreateManifestFile puts ALL the updated notes in!!!!
						// THIS SHOULD BE FIXED NOW!
						WriteManifestFile(true, shareManifest, shareRevision, serverid, theNotes);
					}
					catch (Exception e)
					{
						GtkUtil.ShowHintWindow(new Gtk.Label(), Catalog.GetString("Sharing error"), Catalog.GetString("Error while syncing a share: " + e.Message));
					}
				}

				// FIXME: currently we only return the status of the "normal" manifest.. maybe that's bad :S
				// now the "normal" thing
				return WriteManifestFile(false, manifestFilePath, newRevision, serverid, privateNotes);
			}

			internal bool WriteManifestFile(bool shared, String manifestFilePath, int newRevision, String serverid, Dictionary<String, int> notes)
			{
				String sharedId = null;
				bool success = false;
				MemoryStream buffer = new MemoryStream();
				XmlWriter xml = XmlWriter.Create(buffer, XmlEncoder.DocumentSettings);
				try
				{
					xml.WriteStartDocument();
					xml.WriteStartElement(null, "sync", null);
					xml.WriteAttributeString("revision", newRevision.ToString());
					xml.WriteAttributeString("server-id", serverId);

					foreach (String noteid in notes.Keys)
					{
						string id = noteid;
						string rev = notes[noteid].ToString();

						// check if this is a shared manifest
						if (shared && sharedId == null && shareCopies.ContainsKey(noteid))
						{
							sharedId = noteid;
						}

						// Don't write out deleted notes
						if (deletedNotes.Contains(id))
							continue;

						// we don't to it like this anymore, now we have all updatedNotes already in the notes dictionary
						//// Skip updated notes, we'll update them in a sec
						//if (updatedNotes.Contains(id))
						//  continue;

						xml.WriteStartElement(null, "note", null);
						xml.WriteAttributeString("id", id);
						xml.WriteAttributeString("rev", rev);
						xml.WriteEndElement();
					}

					// we don't to it like this anymore, now we have all updatedNotes already in the notes dictionary
					//// Write out all the updated notes
					//foreach (string uuid in updatedNotes)
					//{
					//  xml.WriteStartElement(null, "note", null);
					//  xml.WriteAttributeString("id", uuid);
					//  xml.WriteAttributeString("rev", newRevision.ToString());
					//  xml.WriteEndElement();
					//}

					if (shared && sharedId != null)
					{
						// write share stuff:
						xml.WriteStartElement(null, "shared", null);
						ShareProvider provider = SecureSharingFactory.Get().GetShareProvider();
						NoteShare share = provider.GetNoteShare(sharedId);
						foreach (String with in share.sharedWith)
						{
							String partner = with;
							if (partner.Contains(" - "))
								partner = partner.Substring(partner.LastIndexOf(" - ") + 3).Trim();

							xml.WriteStartElement(null, "with", null);
							xml.WriteAttributeString("partner", partner);
							xml.WriteEndElement();
						}
						xml.WriteEndElement();
					}

					xml.WriteEndElement();
					xml.WriteEndDocument();

				}
				catch (Exception e)
				{
					Logger.Warn("error while creating manifest! " + e.Message, e);
					success = false;
				}
				finally
				{
					xml.Close();
					
					if (File.Exists(manifestFilePath))
						File.Delete(manifestFilePath);
					// now store in encrypted version:
					if (sharedId != null)
					{
						// this is a shared manifest
						shareSync.EncryptForShare(sharedId, buffer.ToArray(), manifestFilePath);
					}
					else
					{
						SecurityWrapper.SaveAsEncryptedFile(manifestFilePath, buffer.ToArray(), myKey);
					}
					// dispose of plain data
					if (buffer != null)
						buffer.Dispose();
					success = true;
				}

				return success;
			}
			

			#endregion // Private Methods

			#region Private Event Handlers
			private void LockTimeout(object sender, EventArgs args)
			{
				syncLock.RenewCount++;
				UpdateLockFile(syncLock);
				// Reset the timer to 20 seconds sooner than the sync lock duration
				lockTimeout.Reset((uint)syncLock.Duration.TotalMilliseconds - 20000);
			}
			#endregion // Private Event Handlers
		}

}
