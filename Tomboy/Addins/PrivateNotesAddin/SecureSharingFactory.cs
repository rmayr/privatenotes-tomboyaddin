using System;
using System.Collections.Generic;
using System.Text;
using Tomboy.PrivateNotes.Crypto;

namespace Tomboy.PrivateNotes
{

	class SecureSharingFactory
	{
		static 

		internal SecureSharingFactory()
		{
		}

		public static SecureSharingFactory getSecureSharingFactory()
		{

		}

		abstract public ShareProvider GetShareProvider();

		abstract public ShareSync GetShareSync();

		abstract public ShareCryptoFormat GetShareProvider();
		
	}

	class GpgWebDavSharing : SecureSharingFactory
	{
		public override ShareProvider GetShareProvider()
		{
			
		}

		public override ShareSync GetShareSync()
		{
			
		}

		public override ShareCryptoFormat GetShareProvider()
		{

		}
	}
}
