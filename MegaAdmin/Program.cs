using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MegaAdmin
{
	enum Platform
	{
		Windows,
		Linux,
		Mac
	}
	class Program
	{
		public static readonly string version = "0.1";

		public static byte selected { get; private set; } = 0;
		private static byte offset = 0;
		public static readonly List<Server> servers = new List<Server>();
		public static string buffclear = string.Empty;
		private static readonly Thread windowresizewatcher = new Thread(new ThreadStart(() => new WindowResizeWatcherThread()));
		public static readonly Platform RunningPlatform = GetRunningPlatform();

		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
			for (ushort x = 0; x < Console.WindowTop + Console.WindowHeight - 3; x++)
			{
				for (ushort i = 0; i < Console.WindowWidth-1; i++)
				{
					buffclear = buffclear + " ";
				}
				buffclear = buffclear + Environment.NewLine;
			}
			windowresizewatcher.Name = "windowresizewatcher";
			windowresizewatcher.Start();
			Console.Title = "MegaAdmin v" + version;
			startServer();
			while (true)
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				if (servers.Count == 0) { continue; }
					switch (key.Key)
					{
						case ConsoleKey.LeftArrow:
							if (selected != 0)
							{
								selected--;
								WriteBuffer(servers[selected]);
								WriteMenu(servers[selected]);
							}
							break;
						case ConsoleKey.RightArrow:
							if (selected < servers.Count-1)
							{
								selected++;
								WriteBuffer(servers[selected]);
								WriteMenu(servers[selected]);
							}
							break;
						case ConsoleKey.F3:
							//jump to present
							break;
						case ConsoleKey.F4:
							//scroll backwards in time
							break;
						case ConsoleKey.F5:
							//scroll forwards in time
							break;
						default:
							if (servers[selected].cmdlock) { break; }
							if (key.Key == ConsoleKey.Enter)
							{
								servers[selected].CMDevent.Set();
							}
							else
							{
								if (key.Key == ConsoleKey.Backspace)
								{
									if(servers[selected].cmdbuffer.Length > 0)
										servers[selected].cmdbuffer = servers[selected].cmdbuffer.Remove(servers[selected].cmdbuffer.Length - 1);
								}
								else
								{
									servers[selected].cmdbuffer = servers[selected].cmdbuffer + key.KeyChar;
								}
							}
							WriteInput(servers[selected]);
							break;
					}
				//}
				//if (servers.Count == 0) { break; }
			}
		}
		public static int ClampI(int value, int min, int max)
		{
			return (value < min) ? min : (value > max) ? max : value;
		}
		
		public static void startServer()
		{
			Thread server = new Thread(new ThreadStart(() => new Server()));
			server.Name = "server_thread_" + servers.Count;
			server.Start();
		}
		public static void stopServer(Server server)
		{
			if (servers.Count > 1)
			{
				if(selected > 0)
				{
					selected--;
				}
				server.waitHandle.Set();
				WriteBuffer(servers[selected]);
				servers.Remove(server);
				WriteMenu(servers[selected]);
			}
			else
			{
				server.waitHandle.Set();
				Environment.Exit(0);
			}
		}
		private static void ClearLine()
		{
			string str = string.Empty;
			for (ushort i = 0; i < Console.WindowWidth-1; i++)
			{
				str = str + " ";
			}
			Console.Write(str);
		}
		public static void WriteInput(Server server)
		{
			if (servers[selected] != server) { return; }
			Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 2);
			if (server.cmdlock) {
				Console.BackgroundColor = ConsoleColor.DarkRed;
			}
			else
			{
				Console.BackgroundColor = ConsoleColor.DarkBlue;
			}
			
			ClearLine();
			Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 2);
			Console.Write("Enter a Command: ");
			Console.Write(servers[selected].cmdbuffer);
			Console.BackgroundColor = ConsoleColor.Black;
		}
		public static void WriteBuffer(Server server)
		{
			if (servers[selected] != server) { return; }
			int buffoffset = 0;
			int maxh = Console.WindowTop + Console.WindowHeight - 3;
			if (servers[selected].buffer.Count > maxh)
			{
				buffoffset = servers[selected].buffer.Count - maxh;
			}
			string str = string.Empty;
			for (int i = 0 + buffoffset; i < ClampI(maxh+buffoffset, 0, servers[selected].buffer.Count); i++)
			{
				str = str + servers[selected].buffer[i] + Environment.NewLine;
			}
			Console.SetCursorPosition(0, 0);
			Console.Write(buffclear);
			Console.SetCursorPosition(0, 0);
			Console.Write(str);
		}
		public static void WriteMenu(Server server)
		{
			WriteInput(server);
			int maxw = Console.BufferWidth;
//			if (offset < servers.Count && selected == maxw / 19)
//			{
//				offset++;
//			}else if (offset > 0 && selected == offset*19)
//			{
//				offset--;
//			}
			Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);
			ClearLine();
			Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);
			for (byte i = offset; i < ClampI((maxw/19)+offset,0,servers.Count); i++)
			{
				if (i == selected)
				{
					Console.BackgroundColor = ConsoleColor.White;
					Console.ForegroundColor = ConsoleColor.Black;
				}
				Console.Write("<" + servers[i].Name + ">");
				Console.ForegroundColor = ConsoleColor.White;
				Console.BackgroundColor = ConsoleColor.Black;
				Console.Write(" ");
			}
		}

		static void OnProcessExit(object sender, EventArgs e)
		{
			foreach(Server srv in servers)
			{
				if (srv.IsGameProcessRunning)
					srv.Stop();
			}
		}

		private static Platform GetRunningPlatform()
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Unix:
					// Well, there are chances MacOSX is reported as Unix instead of MacOSX.
					// Instead of platform check, we'll do a feature checks (Mac specific root folders)
					if (Directory.Exists("/Applications")
						& Directory.Exists("/System")
						& Directory.Exists("/Users")
						& Directory.Exists("/Volumes"))
						return Platform.Mac;
					else
						return Platform.Linux;

				case PlatformID.MacOSX:
					return Platform.Mac;

				default:
					return Platform.Windows;
			}
		}
	}
}