using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MegaAdmin
{
	class Program
	{
		public static byte selected = 0;
		private static byte offset = 0;
		public static List<Server> servers = new List<Server>();
		public static string buffclear = string.Empty;
		public static Platform platform;

		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
			platform = RunningPlatform();
			for (ushort x = 0; x < Console.WindowTop + Console.WindowHeight - 3; x++)
			{
				for (ushort i = 0; i < Console.WindowWidth-1; i++)
				{
					buffclear = buffclear + " ";
				}
				buffclear = buffclear + Environment.NewLine;
			}
			new Thread(new ThreadStart(() => new WindowResizeWatcherThread())).Start();
			//int lastbuffsize = 0;
			startServer();
			while (true)
			{
				
				//if (lastbuffsize != servers[selected].buffer.Count)
				//{
				//	WriteBuffer();
				//	lastbuffsize = servers[selected].buffer.Count;
				//}
				//System.Threading.Thread.Sleep(1);
				//System.Threading.Thread.Sleep(100);
				//if (Console.KeyAvailable)
				//{
					ConsoleKeyInfo key = Console.ReadKey(true);
					switch (key.Key)
					{
						case ConsoleKey.LeftArrow:
							if (selected != 0)
							{
								selected--;
								WriteBuffer(servers[selected]);
								WriteMenu();
							}
							break;
						case ConsoleKey.RightArrow:
							if (selected < servers.Count-1)
							{
								selected++;
								WriteBuffer(servers[selected]);
								WriteMenu();
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
							if (key.Key == ConsoleKey.Enter)
							{
								servers[selected].cmd();
								WriteInput();
							}
							else
							{
								if (key.Key == ConsoleKey.Backspace)
								{
									servers[selected].cmdbuffer = servers[selected].cmdbuffer.Remove(servers[selected].cmdbuffer.Length - 1);
								}
								else
								{
									servers[selected].cmdbuffer = servers[selected].cmdbuffer + key.KeyChar;
								}
							}
							WriteInput();
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
		private static readonly Random getrandom = new Random();
		private static System.Text.Encoding enc8 = System.Text.Encoding.ASCII;
		public static string GenerateSessionID()
		{
			byte[] buf = new byte[16];
			for (byte i = 0; i < buf.Length; i++)
			{
				byte rnd = (byte)getrandom.Next(0, 3);
				if (rnd == 0)
				{
					buf[i] = (byte)getrandom.Next(48, 58);
				}
				else if (rnd == 1)
				{
					buf[i] = (byte)getrandom.Next(65, 91);
				}
				else if (rnd == 2)

				{
					buf[i] = (byte)getrandom.Next(97, 123);
				}
			}
			return enc8.GetString(buf);
		}
		public static void startServer(string cfgkey = "")
		{
			servers.Add(new Server(cfgkey));
			WriteMenu();
		}
		public static void stopServer(Server server)
		{
			servers.Remove(server);
			if (servers.Count > 0)
			{
				selected--;
				WriteMenu();
				WriteBuffer(servers[selected]);
			}
			else
			{
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
		private static void WriteInput()
		{
			Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 2);
			Console.BackgroundColor = ConsoleColor.DarkBlue;
			ClearLine();
			Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 2);
			Console.Write("Enter a Command: ");
			Console.Write(servers[selected].cmdbuffer);
			Console.BackgroundColor = ConsoleColor.Black;
		}
		public static void WriteBuffer(Server server)
		{
			int buffoffset = 0;
			int maxh = Console.WindowTop + Console.WindowHeight - 3;
			if (server.buffer.Count > maxh)
			{
				buffoffset = servers[selected].buffer.Count - maxh;
			}
			string str = string.Empty;
			for (int i = 0 + buffoffset; i < ClampI(maxh+buffoffset, 0, server.buffer.Count); i++)
			{
				str = str + server.buffer[i];
			}
			Console.SetCursorPosition(0, 0);
			Console.Write(buffclear);
			Console.SetCursorPosition(0, 0);
			Console.Write(str);
		}
		public static void WriteMenu()
		{
			WriteInput();
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
			for (byte i = (byte)servers.Count;i==1;i--)
			{
				servers[i].Stop();
			}
		}

		public enum Platform
		{
			Windows,
			Linux,
			Mac
		}

		public static Platform RunningPlatform()
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