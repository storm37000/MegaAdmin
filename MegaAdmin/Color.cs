namespace MegaAdmin
{
	public static class Color
	{
		public static string Black { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[30m"; } } }

		public static string DarkRed { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[31m"; } } }

		public static string DarkGreen { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[32m"; } } }

		public static string DarkYellow { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[33m"; } } }

		public static string DarkBlue { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[34m"; } } }

		public static string DarkMagenta { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[35m"; } } }

		public static string DarkCyan { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[36m"; } } }

		public static string White { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[37m"; } } }

		public static string Gray { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return White; } } }

		public static string Red = DarkRed; //"\u001b[31m;1m";

		public static string Green = DarkGreen;//"\u001b[32m;1m";

		public static string Yellow = DarkYellow;//"\u001b[33m;1m";

		public static string Blue = DarkBlue;//"\u001b[34m;1m";

		public static string Magenta = DarkMagenta;//"\u001b[35m;1m";

		public static string Cyan = DarkCyan;//"\u001b[36m;1m";

		//public static string BrightWhite = "\u001b[37m;1m";

		public static string Reset { get { if (Program.RunningPlatform == Platform.Windows) { return ""; } else { return "\u001b[0m"; } } }
	}
}
