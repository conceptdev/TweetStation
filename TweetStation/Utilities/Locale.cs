using System;
using MonoTouch.Foundation;

namespace TweetStation
{
	/// <summary>
	/// Use LocalizedString to lookup key-value pairs from .lproj folders
	/// Initial string data extracted using http://github.com/conceptdev/ngenstrings
	/// </summary>
	/// <remarks>
	/// Cannot localize:
	/// * TweetStation.EditAccount.AccountInfo - cannot localize Attribute values
	/// * MonoTouch.Dialog.Controls.RefreshTableHeaderView.SetStatus() - external project
	/// </remarks>
	public static class Locale
	{
		public static string GetText (string str)
		{
			return NSBundle.MainBundle.LocalizedString(str,"");
		}
		
		public static string Format (string fmt, params object [] args)
		{
			fmt = NSBundle.MainBundle.LocalizedString(fmt,"");
			return String.Format (fmt, args);
		}
	}
}
