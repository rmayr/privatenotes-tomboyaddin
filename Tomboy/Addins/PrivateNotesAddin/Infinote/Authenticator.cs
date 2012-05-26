using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Tomboy.PrivateNotes;
using Tomboy.Sync;
using XmppOtrLibrary;

namespace PrivateNotes.Infinote
{
	class Authenticator
	{
		private Communicator com;

		public event MessageCallback OnSendAuthMsg;

		private Dictionary<String, Authd> authd = new Dictionary<String, Authd>();

		public Authenticator(Communicator com)
		{
			this.com = com;
		}

		public void Reset()
		{
			authd = new Dictionary<String, Authd>();
		}

		public void Authenticate(String toUser, byte[] localAuthData, byte[] remoteAuthData)
		{
			var creator = new SelfAuthenticator(toUser, localAuthData);
			creator.OnDone += new UserAuthenticationDone(selfAuth_OnDone);
			ThreadPool.QueueUserWorkItem(creator.DoWork);
		}

		void  selfAuth_OnDone(string user, bool success, string authMsg)
		{
			if (!success)
			{
				Logger.Error("creating our auth mgs failed!");
				return;
			}
			// success
			var a = GetAuthdObj(user);
			a.Local = true;
			if (a.Done)
			{
				Logger.Info("Authenticating user " + user + " finished");
			}
			OnSendAuthMsg(user, "AUTH:" + authMsg);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fromUser"></param>
		/// <param name="remoteKeyData">the remote key data</param>
		/// <param name="msg">the received auth msg (including "AUTH:")</param>
		public void OnAuthMsgReceived(String fromUser, byte[] remoteKeyData, String msg)
		{
			// verify
			msg = msg.Substring("AUTH:".Length);
			var creator = new UserAuthenticator(fromUser, remoteKeyData, msg);
			creator.OnDone += remoteAuth_OnDone;
			ThreadPool.QueueUserWorkItem(creator.DoWork);
		}

		void remoteAuth_OnDone(string user, bool success, string authMsg)
		{
			if (!success)
			{
				GtkUtil.ShowHintWindow("XMPP User Error", "The XMPP user \"" + user +"\" could not be authenticated!\n"+
					"Check if you have set the correct Key id in your mapping. You cannot communicate with this user unless the mapping is correct.\n"+
					"If the mapping is correct, this could also be an attempted attack.");
				Logger.Error("authenticating remote user failed!");
				return;
			}
			// success
			var a = GetAuthdObj(user);
			a.Remote = true;
			if (a.Done)
			{
				Logger.Info("Authenticating user " + user + " finished");
			}
		}

		/// <summary>
		/// remove a user, e.g. when he goes offline
		/// </summary>
		/// <param name="user"></param>
		public void RemoveUser(String user)
		{
			authd.Remove(user);
		}
		
		/// <summary>
		/// checks if mutual authentication has finished
		/// </summary>
		/// <param name="user"></param>
		/// <returns></returns>
		public bool IsAuthenticated(String user)
		{
			Authd a = null;
			if (authd.TryGetValue(user, out a))
			{
				return a.Done;
			}
			return false;
		}

		private Authd GetAuthdObj(String user)
		{
			Authd a = null;
			if (!authd.TryGetValue(user, out a))
			{
				a = new Authd();
				authd.Add(user, a);
			}
			return a;
		}

	}

	/// <summary>
	/// holds flags for remote and local authentication
	/// used to check if already mutually authenticated
	/// </summary>
	class Authd
	{
		public Authd()
		{
			Local = false;
			Remote = false;
		}

		public bool Done
		{
			get { return Local && Remote; }
		}

		public bool Local { get; set; }
		public bool Remote { get; set; }
	}

	/// <summary>
	/// callback to tell when a connection was successfully secured
	/// </summary>
	/// <param name="user"></param>
	/// <param name="signed"></param>
	internal delegate void UserAuthenticationDone(String user, bool success, String signed);

	/// <summary>
	/// create our own authentication message for sending to another user
	/// </summary>
	class SelfAuthenticator
	{

		public event UserAuthenticationDone OnDone;
		private String user;
		private byte[] data;

		public SelfAuthenticator(String user, byte[] localKeyData)
		{
			this.user = user;
			this.data = localKeyData;
		}

		public void DoWork(Object threadContext)
		{
			String signMe = Util.ByteArrayToHexString(data);
			String signed = SecurityWrapper.Sign(signMe);
			OnDone(user, true, signed);
		}
	}

	/// <summary>
	/// verify the authentication message from another user
	/// </summary>
	class UserAuthenticator
	{

		public event UserAuthenticationDone OnDone;
		private String user;
		private byte[] data;
		private String received;

		public UserAuthenticator(String user, byte[] remoteKeyData, String receivedData)
		{
			this.user = user;
			this.received = receivedData;
			this.data = remoteKeyData;
		}

		public void DoWork(Object threadContext)
		{
			String shouldBe = Util.ByteArrayToHexString(data);
			// get the sender:
			var entry = Communicator.Instance.AddressProvider.GetEntryForXmppId(user);
			if (entry == null)
			{
				Logger.Warn("cannot check auth, unknown user!");
				OnDone(user, false, String.Empty);
			}
			String gpgId = entry.Person.id;
			String userId = Util.GetUserIdFromGpgId(gpgId);

			bool ok = SecurityWrapper.VerifySignature(received, shouldBe, userId);

			OnDone(user, ok, String.Empty);
		}
	}


}
