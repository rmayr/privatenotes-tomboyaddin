// Permission is hereby granted, free of charge, to any person obtaining 
// a copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions: 
//  
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software. 
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
// 
// Copyright (c) 2008 Novell, Inc. (http://www.novell.com) 
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Tomboy.Sync;
using Tomboy.WebSync.Api;

namespace Tomboy.WebSync
{
	public class WebSyncServer : SyncServer
	{
		private string serverUrl;
		private string userName;

		private IAuthProvider auth;
		
		private UserInfo user;
		private List<NoteInfo> pendingCommits;
		
		public WebSyncServer (string serverUrl, string userName, string password)
		{
			this.serverUrl = serverUrl;
			this.userName = userName;

			auth = new BasicHttpAuthProvider (userName, password);
		}

		#region SyncServer implementation
		
		public bool BeginSyncTransaction ()
		{
			// TODO: Check connection and auth (is getting user resource a sufficient check?)
			user = UserInfo.GetUser (serverUrl, userName, auth);
			if (user.LatestSyncRevision.HasValue)
				LatestRevision = user.LatestSyncRevision.Value;
			else
				VerifyLatestSyncRevision (user.LatestSyncRevision);
			
			pendingCommits = new List<NoteInfo> ();
			return true;
		}
		
		public bool CancelSyncTransaction ()
		{
			// TODO: Cancel any pending request
			pendingCommits.Clear ();
			return true;
		}
		
		public bool CommitSyncTransaction ()
		{
			if (pendingCommits != null && pendingCommits.Count > 0) {
				LatestRevision = user.UpdateNotes (pendingCommits, LatestRevision + 1);
				pendingCommits.Clear ();
			}
			return true;
		}
		
		public SyncLockInfo CurrentSyncLock {
			get {
				return null;
			}
		}
		
		public void DeleteNotes (IList<string> deletedNoteUUIDs)
		{
			foreach (string uuid in deletedNoteUUIDs) {
				NoteInfo noteInfo = new NoteInfo ();
				noteInfo.Command = "delete";
				noteInfo.Guid = uuid;
				pendingCommits.Add (noteInfo);
			}
		}
		
		public IList<string> GetAllNoteUUIDs ()
		{
			List<string> uuids = new List<string> ();
			int? latestRevision;
			IList<NoteInfo> serverNotes = user.GetNotes (false, out latestRevision);
			VerifyLatestSyncRevision (latestRevision);
			foreach (NoteInfo noteInfo in serverNotes)
				uuids.Add (noteInfo.Guid);
			return uuids;
		}
		
		public IDictionary<string, NoteUpdate> GetNoteUpdatesSince (int revision)
		{
			Dictionary<string, NoteUpdate> updates =
				new Dictionary<string, NoteUpdate> ();
			int? latestRevision;
			IList<NoteInfo> serverNotes = user.GetNotes (true, revision, out latestRevision);
			VerifyLatestSyncRevision (latestRevision);
			foreach (NoteInfo noteInfo in serverNotes) {
				string noteXml = NoteConvert.ToNoteXml (noteInfo);
				NoteUpdate update = new NoteUpdate (noteXml,
				                                    noteInfo.Title,
				                                    noteInfo.Guid,
				                                    noteInfo.LastSyncRevision.Value);
				updates.Add (noteInfo.Guid, update);
			}
			return updates;
		}
		
		public string Id {
			get {
				return serverUrl;
			}
		}
		
		public int LatestRevision { get; private set; }
		
		public void UploadNotes (IList<Note> notes)
		{
			foreach (Note note in notes)
				pendingCommits.Add (NoteConvert.ToNoteInfo (note));
		}
		
		#endregion

		#region Private Methods

		private void VerifyLatestSyncRevision (int? latestRevision)
		{
			if (!latestRevision.HasValue)
				throw new TomboySyncException ("No sync revision provided in server response");
			if (latestRevision.Value != LatestRevision)
				throw new TomboySyncException ("Latest revision on server has changed, please update restart your sync");
		}

		#endregion
	}
}