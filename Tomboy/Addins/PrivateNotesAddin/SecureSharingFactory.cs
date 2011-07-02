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

		/// <summary>
		/// gets the share provider which is responsible for locally managing all shares
		/// </summary>
		/// <returns></returns>
		abstract public ShareProvider GetShareProvider();

		/// <summary>
		/// gets the ShareSync object which is responsible for communicating with the sharing services
		/// </summary>
		/// <returns></returns>
		abstract public ShareSync GetShareSync();

		/// <summary>
		/// gets the share crypto format which may be used by the shared sync to encrypt elements
		/// for multiple recipients
		/// </summary>
		/// <returns></returns>
		abstract public ShareCryptoFormat GetShareCrypto();

		/// <summary>
		/// gets the normal crypto instance which is responsible for symmetric cryptography
		/// e.g. encrypting elements that are only used by ourselves
		/// </summary>
		/// <returns></returns>
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

		/// <summary>
		/// <inheritdoc />
		/// </summary>
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

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public override ShareCryptoFormat GetShareCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.gpgCrypt) as ShareCryptoFormat;
		}

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public override CryptoFormat GetCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.gpgCrypt);
		}
	}

	/// <summary>
	/// This provides a wrapper for GpgWebDavSharing which can be used if we need to
	/// debug the sync process or similar. It does the same sync things but isntead of using
	/// gpg encryption it does NO ENCRYPTION at all!!! so be VERY careful when using this!
	/// </summary>
	class UnencryptedWebDavSharing : GpgWebDavSharing 
	{
		internal UnencryptedWebDavSharing()
		{
		}

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public override ShareCryptoFormat GetShareCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.nullCrypt) as ShareCryptoFormat;
		}

		/// <summary>
		/// <inheritdoc />
		/// </summary>
		public override CryptoFormat GetCrypto()
		{
			return CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat(CryptoFormatProviderFactory.CryptoFormatType.nullCrypt);
		}
	}
}
