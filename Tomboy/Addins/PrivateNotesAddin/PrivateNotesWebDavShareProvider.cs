using System;
using System.Collections.Generic;
using System.Text;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// implementation of the shareProvider which actually queires the privateNotes server
	/// for a new webdav storage folder for a shared note
	/// </summary>
	class PrivateNotesWebDavShareProvider : WebDavShareProvider
	{
		internal override NoteShare CreateNewShare(String noteuid, String shareWith)
		{
			String privateNotesServer = "193.170.124.44";
			// request a new share:
			String requestUrl = "http://" + privateNotesServer + "/account/newShare.php";
			String response = Util.HttpGet(requestUrl);
			string user = null;
			string pass = null;
			bool ok = ParseResponse(response, out user, out pass);

			if (!ok)
				throw new Exception("Could not obtain new share path from PrivateNotes server!");


			String sharePath = "https://" + user + ":" + pass + "@" + privateNotesServer + "/webdav2/" + user;
			return new NoteShare(noteuid, shareWith, sharePath);
		}

		/// <summary>
		/// parse the server response for the new share-space request
		/// </summary>
		/// <param name="response"></param>
		/// <param name="user"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		private bool ParseResponse(string response, out string user, out string password)
		{
			Hyena.Json.Deserializer deserializer =
				new Hyena.Json.Deserializer(response);
			object obj = deserializer.Deserialize();
			Hyena.Json.JsonObject jsonObj =
				obj as Hyena.Json.JsonObject;

			user = null;
			password = null;

			if (jsonObj == null)
				return false;

			object val = null;

			bool ok = false;
			if (jsonObj.TryGetValue("ok", out val))
				ok = (bool)val;
			else
				return false;

			if (!ok)
			{
				String errorMsg = null;
				val = null;
				if (jsonObj.TryGetValue("errorMsg", out val))
					errorMsg = (string)val;
				if (errorMsg != null)
					throw new Exception("Creating a new share space failed with the following error: " + errorMsg);
				else
					return false; // simply return false, because we don't know more
			}

			val = null;
			if (jsonObj.TryGetValue("user", out val))
				user = (string)val;
			else
				return false;

			val = null;
			if (jsonObj.TryGetValue("password", out val))
				password = (string)val;
			else
				return false;

			return true;
		}
	}
}
