using System;
using MonoTouch.Foundation;

namespace TweetStation
{
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
