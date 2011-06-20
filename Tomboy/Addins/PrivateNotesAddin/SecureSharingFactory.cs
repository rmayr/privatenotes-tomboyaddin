using System;
using System.Collections.Generic;
using System.Text;
using Tomboy.PrivateNotes.Crypto;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// factory class (abstract factory pattern) which gives you a
	/// factory that can create all the necessary objects for shared
	/// syncing and is very configurable
	/// we could easily switch out any of those objects
	/// </summary>
	abstract class SecureSharingFactory
	{
		static SecureSharingFactory defaultSharing;

		/// <summary>
		/// hide constructor to only allow static access
		/// </summary>
		internal SecureSharingFactory()
		{
		}

		public static SecureSharingFactory Get()
		{
			if (defaultSharing == null)
			{
#if NOCRYPT
				defaultSharing = new UnencryptedWebDavSharing();
#elif OLDCRYPT
				// still react differently to OLDCRYPT to easier detect the bug
				// when we enter it by mistake
				throw new Exception("not supported");
#else
				defaultSharing = new GpgWebDavSharing();
#endif
			}
				
			return defaultSharing;
		}

		abstract public ShareProvider GetShareProvider();

		abstract public ShareSync GetShareSync();

		abstract public ShareCryptoFormat GetShareCrypto();

		abstract public CryptoFormat GetCrypto();
		
	}

	class GpgWebDavSharing : SecureSharingFactory
	{
		internal GpgWebDavSharing()
		{
		}

		static WebDavShareProvider provider = null;

		public override ShareProvider GetShareProvider()
		{
			if (provider == null)
			{
				//provider = new WebDavShareProvider();
				provider = new PrivateNotesWebDavShareProvider();
			}
			return provider;
		}

		public override ShareSync GetShareSync()
		{
			// this also checks if we have a compatible provider
			ShareProvider theProvider = GetShareProvider();
			if (theProvider is WebDavShareProvider)
			{
				return WebDAVShareSync.GetInstance(theProvider);
			} else {
				Logger.Warn("no sharesync for provider of type " + theProvider.GetType().Name);
				throw new NotImplementedException("no sharesync for provider of type " + theProvider.GetType().Name);
			}
		}

		public override ShareCryptoFormat GetShareCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.gpgCrypt) as ShareCryptoFormat;
		}

		public override CryptoFormat GetCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.gpgCrypt);
		}
	}

	class UnencryptedWebDavSharing : GpgWebDavSharing 
	{
		internal UnencryptedWebDavSharing()
		{
		}

		public override ShareCryptoFormat GetShareCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.nullCrypt) as ShareCryptoFormat;
		}

		public override CryptoFormat GetCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.nullCrypt);
		}
	}
}
