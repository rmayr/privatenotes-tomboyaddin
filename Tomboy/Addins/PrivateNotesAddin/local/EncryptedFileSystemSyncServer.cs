using System;
using System.Collections.Generic;
using System.Text;
using Tomboy;
using System.IO;
using System.Xml;
using Tomboy.PrivateNotes.Crypto;
using Tomboy.PrivateNotes;

namespace Tomboy.Sync
{
		public class EncryptedFileSystemSyncServer : SyncServer
		{
			private byte[] myKey;
			// for synchronization with shared storages
			internal ShareSync shareSync;
			internal Dictionary<String, DirectoryInfo> shareCopies = null;

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
				ShareProvider provider = EncryptedWebdavSyncServiceAddin.shareProvider;
				shareSync = ShareSyncFactory.GetShareSyncForProvider(provider);

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
					String id = GetNoteIdFromFileName(pathToNote);
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

			/// <summary>
			/// utility method which parses the note id from the filename
			/// </summary>
			/// <param name="fileName"></param>
			/// <returns></returns>
			public String GetNoteIdFromFileName(String fileName)
			{
				String noteid = null;
				if (fileName.EndsWith(".note"))
				{
					FileInfo file = new System.IO.FileInfo(fileName);
					noteid = file.Name.Replace(".note", "");
				}
				else
					Logger.Warn("filename not a note! {0}", fileName);
				return noteid;
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
						string serverNotePath = Path.Combine(newRevisionPath, Path.GetFileName(note.FilePath));
						SecurityWrapper.CopyAndEncrypt(note.FilePath, serverNotePath, myKey);
						//File.Copy(note.FilePath, serverNotePath, true);

						// upload to webdav takes place in commit-function
						AdjustPermissions(serverNotePath);
						updatedNotes.Add(Path.GetFileNameWithoutExtension(note.FilePath));
					}
					catch (Exception e)
					{
						Logger.Error("Sync: Error uploading note \"{0}\": {1}", note.Title, e.Message);
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
				/* // THIS IS THE OLD VERSION, DON'T USE THIS
				if (IsValidXmlFile(manifestPath))
				{
					// TODO: Permission errors
					using (FileStream fs = new FileStream(manifestPath, FileMode.Open))
					{
						bool ok;
						Stream plainStream = SecurityWrapper.DecryptFromStream(manifestPath, fs, myKey, out ok);
						if (!ok)
							throw new EncryptionException("ENCRYPTION ERROR");

						XmlDocument doc = new XmlDocument();
						doc.Load(plainStream);

						XmlNodeList noteIds = doc.SelectNodes("//note/@id");
						Logger.Debug("GetAllNoteUUIDs has {0} notes", noteIds.Count);
						foreach (XmlNode idNode in noteIds)
						{
							noteUUIDs.Add(idNode.InnerText);
						}
					}
				}
				*/
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

				foreach (string id in updates.Keys)
				{
					int rev = updates[id];
					if (noteUpdates.ContainsKey(id) == false)
					{
						// Copy the file from the server to the temp directory
						string revDir = GetRevisionDirPath(rev);
						string serverNotePath = GetNotePath(revDir, id);
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
							CryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();
							byte[] contents = ccf.DecryptFile(serverNotePath, myKey, out ok);
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
					// TODO: error-checking, etc
					string manifestFilePath = Path.Combine(newRevisionPath,
																									"manifest.xml");
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
						// overwrite if already in there, else add
						if (allNotes.ContainsKey(id))
							allNotes[id] = newRevision;
						else
							allNotes.Add(id, newRevision);
					}


					/* ****** REMOVE THIS AS SOON AS WE KNOW THAT IT WORKS
					XmlWriter xml = XmlWriter.Create(plainBuf, XmlEncoder.DocumentSettings);
					try
					{
						xml.WriteStartDocument();
						xml.WriteStartElement(null, "sync", null);
						xml.WriteAttributeString("revision", newRevision.ToString());
						xml.WriteAttributeString("server-id", serverId);

						foreach (XmlNode node in noteNodes)
						{
							string id = node.SelectSingleNode("@id").InnerText;
							string rev = node.SelectSingleNode("@rev").InnerText;

							// Don't write out deleted notes
							if (deletedNotes.Contains(id))
								continue;

							// Skip updated notes, we'll update them in a sec
							if (updatedNotes.Contains(id))
								continue;

							xml.WriteStartElement(null, "note", null);
							xml.WriteAttributeString("id", id);
							xml.WriteAttributeString("rev", rev);
							xml.WriteEndElement();
						}

						// Write out all the updated notes
						foreach (string uuid in updatedNotes)
						{
							xml.WriteStartElement(null, "note", null);
							xml.WriteAttributeString("id", uuid);
							xml.WriteAttributeString("rev", newRevision.ToString());
							xml.WriteEndElement();
						}

						xml.WriteEndElement();
						xml.WriteEndDocument();
					 */
					bool manifestCreated = CreateManifestFile(manifestFilePath, newRevision, serverId, allNotes);
					if (!manifestCreated)
						throw new Exception("could not create manifest file, cannot recover from this error.");

					AdjustPermissions(manifestFilePath);
#endregion

					// only use this if we use the revision-folder-mode
					if (!manifestFilePath.Equals(manifestPath))
					{
#region DIR_VERSION
						// WARNING! THIS VERSION IS NO LONGER SUPPORTED...
						// SINCE WE DON'T NEED IT FOR OUR CURRENT WEBDAV SYNC
						// IT IS PROBABLY OUT OF DATE! IF YOU WANT TO USE IT AGAIN
						// MAKE SURE EVERYTHING WORKS
						// Rename original /manifest.xml to /manifest.xml.old
						string oldManifestPath = manifestPath + ".old";
						if (File.Exists(manifestPath) == true)
						{
							if (File.Exists(oldManifestPath))
							{
								File.Delete(oldManifestPath);
							}
							File.Move(manifestPath, oldManifestPath);
						}


						// * * * Begin Cleanup Code * * *
						// TODO: Consider completely discarding cleanup code, in favor
						//			 of periodic thorough server consistency checks (say every 30 revs).
						//			 Even if we do continue providing some cleanup, consistency
						//			 checks should be implemented.

						// Copy the /${parent}/${rev}/manifest.xml -> /manifest.xml
						// don't encrypt here because file is already encrypted!
						//SecurityWrapper.CopyAndEncrypt(manifestFilePath, manifestPath, myKey);
						File.Copy(manifestFilePath, manifestPath, true);
						AdjustPermissions(manifestPath);

						try
						{
							// Delete /manifest.xml.old
							if (File.Exists(oldManifestPath))
								File.Delete(oldManifestPath);

							string oldManifestFilePath = Path.Combine(GetRevisionDirPath(newRevision - 1),
																					 "manifest.xml");

							if (File.Exists(oldManifestFilePath))
							{
								// TODO: Do step #8 as described in http://bugzilla.gnome.org/show_bug.cgi?id=321037#c17
								// Like this?
								FileInfo oldManifestFilePathInfo = new FileInfo(oldManifestFilePath);
								foreach (FileInfo file in oldManifestFilePathInfo.Directory.GetFiles())
								{
									string fileGuid = Path.GetFileNameWithoutExtension(file.Name);
									if (deletedNotes.Contains(fileGuid) ||
																	updatedNotes.Contains(fileGuid))
										File.Delete(file.FullName);
									// TODO: Need to check *all* revision dirs, not just previous (duh)
									//			 Should be a way to cache this from checking earlier.
								}

								// TODO: Leaving old empty dir for now.	Some stuff is probably easier
								//			 when you can guarantee the existence of each intermediate directory?

							}
						}
						catch (Exception e)
						{
							Logger.Error("Exception during server cleanup while committing. " +
														"Server integrity is OK, but there may be some excess " +
														"files floating around.	Here's the error:\n" +
														e.Message);
						}
#endregion
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
								}

								if (updatedNotes.Contains(fileGuid))
								{
									Logger.Info("uploading " + fileGuid);
									OnUploadFile(file.FullName);
								}
							}
						}
						catch (Exception e)
						{
							Logger.Error("Exception during server cleanup while committing. " +
														"Server integrity is OK, but there may be some excess " +
														"files floating around.	Here's the error:\n" +
														e.Message);
						}

					}
				}
				else
				{
					// no changes (no updates/deletes)
				}

				lockTimeout.Cancel();
				RemoveLockFile(lockPath);
				commitSucceeded = true;// TODO: When return false?
				return commitSucceeded;
			}

			// TODO: Return false if this is a bad time to cancel sync?
			public virtual bool CancelSyncTransaction()
			{
				lockTimeout.Cancel();
				RemoveLockFile(lockPath);
				
				// clean up sync share
				if (shareSync != null)
				{
					shareSync.CleanUp();
				}

				return true;
			}

			public virtual int LatestRevision
			{
				get
				{
					int latestRev = -1;
					int latestRevDir = -1;
					latestRev = GetRevisionFromManifestFile(manifestPath);

					// now check the shared notes
					shareCopies = shareSync.GetShareCopies();
					foreach (DirectoryInfo di in shareCopies.Values)
					{
						int revision = GetRevisionFromManifestFile(Path.Combine(di.FullName, "manifest.xml"));

						if (revision > latestRev)
							latestRev = revision;
					}

					bool foundValidManifest = false;
					while (!foundValidManifest)
					{
						if (latestRev < 0)
						{
							// Look for the highest revision parent path
							foreach (string dir in Directory.GetDirectories(serverPath))
							{
								try
								{
									int currentRevParentDir = Int32.Parse(Path.GetFileName(dir));
									if (currentRevParentDir > latestRevDir)
										latestRevDir = currentRevParentDir;
								}
								catch { }
							}

							if (latestRevDir >= 0)
							{
								foreach (string revDir in Directory.GetDirectories(
																 Path.Combine(serverPath, latestRevDir.ToString())))
								{
									try
									{
										int currentRev = Int32.Parse(revDir);
										if (currentRev > latestRev)
											latestRev = currentRev;
									}
									catch { }
								}
							}

							if (latestRev >= 0)
							{
								// Validate that the manifest file inside the revision is valid
								// TODO: Should we create the /manifest.xml file with a valid one?
								string revDirPath = GetRevisionDirPath(latestRev);
								string revManifestPath = Path.Combine(revDirPath, "manifest.xml");
								if (IsValidXmlFile(revManifestPath))
									foundValidManifest = true;
								else
								{
									// TODO: Does this really belong here?
									Directory.Delete(revDirPath, true);
									// Continue looping
								}
							}
							else
								foundValidManifest = true;
						}
						else
							foundValidManifest = true;
					}

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
			
			virtual internal Dictionary<String, int> GetNoteUpdatesIdsSince(int revision)
			{
				Dictionary<String, int> updates = GetNoteRevisionsFromManifest(manifestPath, revision);
				List<String> processedFiles = new List<string>();
				foreach (DirectoryInfo dir in shareCopies.Values)
				{
					String path = Path.Combine(dir.FullName, "manifest.xml");
					if (!processedFiles.Contains(path))
					{
						// only processed if not already done
						processedFiles.Add(path);
						Dictionary<String, int> addme = GetNoteRevisionsFromManifest(path, revision);
						foreach (String key in addme.Keys)
						{
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
								// it's ok, it's a shared note we know about
								if (updates.ContainsKey(key))
								{
									if (updates[key] < addme[key]) // overwrite if the one from the shared folder is newer
										updates[key] = addme[key];
								}
								else
									updates.Add(key, addme[key]);
							}
						}
					}
				}

				return updates;
			}

			virtual internal Dictionary<String, int> GetNoteRevisionsFromManifest(String filePath, int revision)
			{
				Dictionary<String, int> updates = new Dictionary<String, int>();
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
				return Path.Combine(
											 Path.Combine(serverPath, (rev / 100).ToString()),
											 rev.ToString());
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

			internal int GetRevisionFromManifestFile(String path)
			{
				int latestRev = -1;
				if (IsValidXmlFile(path) == true)
				{
					using (FileStream fs = new FileStream(path, FileMode.Open))
					{
						bool ok;
						Stream plainStream = SecurityWrapper.DecryptFromStream(path, fs, myKey, out ok);
						if (!ok)
							throw new Exception("ENCRYPTION ERROR!");

						XmlDocument doc = new XmlDocument();
						doc.Load(plainStream);
						XmlNode syncNode = doc.SelectSingleNode("//sync");
						string latestRevStr = syncNode.Attributes.GetNamedItem("revision").InnerText;
						if (latestRevStr != null && latestRevStr != string.Empty)
							latestRev = Int32.Parse(latestRevStr);
					}
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
					}
				}

				// now create the additional manifests:
				foreach (String shareManifest in additionalManifests.Keys)
				{
					Dictionary<String, int> theNotes = new Dictionary<string, int>();
					List<String> ids = additionalManifests[shareManifest];
					foreach (String id in ids)
					{
						theNotes.Add(id, notes[id]);
					}
					// TODO FIXME FATAL this doesn't work, because base.CreateManifestFile puts ALL the updated notes in!!!!
					// THIS SHOULD BE FIXED NOW!
					WriteManifestFile(shareManifest, newRevision, serverid, theNotes);
				}

				// XXX: currently we only return the status of the "normal" manifest.. maybe that's bad :S
				// now the "normal" thing
				return WriteManifestFile(manifestFilePath, newRevision, serverid, notes);
			}

			internal bool WriteManifestFile(String manifestFilePath, int newRevision, String serverid, Dictionary<String, int> notes)
			{
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
					// now store in encrypted version:
					SecurityWrapper.SaveAsEncryptedFile(manifestFilePath, buffer.ToArray(), myKey);
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
