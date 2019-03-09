using System;//why is a bullet here?
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace MegaAdmin
{
	class OutputThread
	{
		public readonly Regex SMOD_REGEX = new Regex(@"\[(DEBUG|INFO|WARN|ERROR)\] (\[.*?\]) (.*)", RegexOptions.Compiled | RegexOptions.Singleline);
		public readonly string DEFAULT_FOREGROUND = Color.Cyan;
		public readonly string DEFAULT_BACKGROUND = Color.Black;

		public OutputThread(Server server)
		{
			FileSystemWatcher watcher = new FileSystemWatcher("SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + server.SID, "sl*.mapi");
			watcher.IncludeSubdirectories = false;

			//if (Program.platform == Program.Platform.Linux)
			//{
				ReadLinux(server, watcher);
			//}
			//else
			//{
			//	ReadWindows(server, watcher);
			//}
		}

		public void ReadWindows(Server server, FileSystemWatcher watcher)
		{
			watcher.Changed += new FileSystemEventHandler((sender, eventArgs) => OnDirectoryChanged(sender, eventArgs, server));
			watcher.EnableRaisingEvents = true;
		}

		public void ReadLinux(Server server, FileSystemWatcher watcher)
		{
			watcher.Created += new FileSystemEventHandler((sender, eventArgs) => OnMapiCreated(sender, eventArgs, server));
			watcher.EnableRaisingEvents = true;
		}

		private void OnDirectoryChanged(object source, FileSystemEventArgs e, Server server)
		{
			if (!Directory.Exists(e.FullPath))
			{
				return;
			}
			string[] files = Directory.GetFiles(e.FullPath, "sl*.mapi", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToArray<string>();
			foreach (string file in files)
			{
				ProcessFile(server, file);
			}
		}

		private void OnMapiCreated(object source, FileSystemEventArgs e, Server server)
		{
			Thread.Sleep(15);
			ProcessFile(server, e.FullPath);
		}

		private void ProcessFile(Server server, string file)
		{
			string stream = string.Empty;
			string command = "open";
			int attempts = 0;
			bool read = false;

			while (attempts < (server.runOptimized ? 10 : 100) && !read && !server.stopping)
			{
				try
				{
					if (!File.Exists(file))
					{
						// The file definitely existed at the moment Change event was raised by OS
						// If the file is not here after 15 ms that means that
						// (a) either it was already processed
						// (b) it was deleted by some other application
						return;
					}

					StreamReader sr = new StreamReader(file);
					stream = sr.ReadToEnd();
					command = "close";
					sr.Close();
					command = "delete";
					File.Delete(file);
					read = true;
				}
				catch
				{
					attempts++;
					if (attempts >= (server.runOptimized ? 10 : 100))
					{
						server.write("Message printer warning: Could not " + command + " " + file + ". Make sure that MultiAdmin.exe has all necessary read-write permissions.", Color.Yellow);
						server.write("skipping", Color.Yellow);
					}
				}
			}

			if (server.stopping) return;

			bool display = true;
			string color = Color.Cyan;

			if (!string.IsNullOrEmpty(stream.Trim()))
			{
				if (stream.Contains("LOGTYPE"))
				{
					string type = stream.Substring(stream.IndexOf("LOGTYPE")).Trim();
					stream = stream.Substring(0, stream.IndexOf("LOGTYPE")).Trim();

					switch (type)
					{
						case "LOGTYPE02":
							color = Color.Green;
							break;
						case "LOGTYPE-8":
							color = Color.DarkRed;
							break;
						case "LOGTYPE14":
							color = Color.Magenta;
							break;
						default:
							color = Color.Cyan;
							break;
					}
				}
			}

			string[] streamSplit;

			// Smod2 loggers pretty printing
			var match = SMOD_REGEX.Match(stream);
			if (match.Success)
			{
				if (match.Groups.Count >= 2)
				{
					string levelColor = Color.Cyan;
					string tagColor = Color.Yellow;
					string msgColor = Color.White;
					switch (match.Groups[1].Value.Trim())
					{
						case "DEBUG":
							levelColor = Color.Gray;
							break;
						case "INFO":
							levelColor = Color.Green;
							break;
						case "WARN":
							levelColor = Color.DarkYellow;
							break;
						case "ERROR":
							levelColor = Color.Red;
							msgColor = Color.Red;
							break;
						default:
							color = Color.Cyan;
							break;
					}

					lock (server)
					{
						server.writePart(string.Empty, Color.Cyan, true, false);
						server.writePart("[" + match.Groups[1].Value + "] ", levelColor, false, false);
						server.writePart(match.Groups[2].Value + " ", tagColor, false, false);
						server.writePart(match.Groups[3].Value, msgColor, false, true);
					}

					server.Log("[" + match.Groups[1].Value + "] " + match.Groups[2].Value + " " + match.Groups[3].Value);

					// P.S. the format is [Info] [courtney.exampleplugin] Something interesting happened
					// That was just an example
					display = false;

					// Limiting output speed for Smod messages
					Thread.Sleep(server.printSpeed);

					// This return should be here
					return;
				}
			}

			if (stream.Contains("Server starting at all IP addresses and port"))
			{
				string str = stream.Replace("Server starting at all IP addresses and port ", string.Empty);
				server.Port = ushort.Parse(str.Trim());
			}

			if (stream.Contains("Mod Log:"))
			{
				foreach (IEventAdminAction Event in server.adminaction)
				{
					Event.OnAdminAction(stream.Replace("Mod log:", string.Empty));
				}
			}

			if (stream.Contains("ServerMod - Version"))
			{
				server.HasServerMod = true;
				// This should work fine with older ServerMod versions too
				streamSplit = stream.Replace("ServerMod - Version", string.Empty).Split('-');
				server.ServerModVersion = streamSplit[0].Trim();
				server.ServerModBuild = (streamSplit.Length > 1 ? streamSplit[1] : "A").Trim();
			}

			if (server.ServerModCheck(1, 7, 2))
			{
				if (stream.Contains("Round restarting"))
				{
					foreach (IEventRoundEnd Event in server.roundend)
					{
						Event.OnRoundEnd();
					}
				}

				if (stream.Contains("Waiting for players"))
				{
					if (!server.InitialRoundStarted)
					{
						server.InitialRoundStarted = true;
						foreach (IEventRoundStart Event in server.roundstart)
						{
							Event.OnRoundStart();
						}
					}

					if (server.ServerModCheck(1, 5, 0) && server.fixBuggedPlayers)
					{
						server.SendMessage("ROUNDRESTART");
						server.fixBuggedPlayers = false;
					}
				}
			}
			else
			{
				if (stream.Contains("Waiting for players"))
				{
					if (!server.InitialRoundStarted)
					{
						server.InitialRoundStarted = true;
						foreach (IEventRoundStart Event in server.roundstart)
						{
							Event.OnRoundStart();
						}
					}
					else
					{
						foreach (IEventRoundEnd Event in server.roundend)
						{
							Event.OnRoundEnd();
						}
					}

					if (server.ServerModCheck(1, 5, 0) && server.fixBuggedPlayers)
					{
						server.SendMessage("ROUNDRESTART");
						server.fixBuggedPlayers = false;
					}
				}
			}


			if (stream.Contains("New round has been started"))
			{
				foreach (IEventRoundStart Event in server.roundstart)
				{
					Event.OnRoundStart();
				}
			}

			if (stream.Contains("Level loaded. Creating match..."))
			{
				foreach (IEventServerStart Event in server.serverstart)
				{
					Event.OnServerStart();
				}
			}


			if (stream.Contains("Server full"))
			{
				foreach (IEventServerFull Event in server.serverfull)
				{
					Event.OnServerFull();
				}
			}


			if (stream.Contains("Player connect"))
			{
				display = false;
				foreach (IEventPlayerConnect Event in server.playerconnect)
				{
					string name = stream.Substring(stream.IndexOf(":"));
					Event.OnPlayerConnect(name);
				}
			}

			if (stream.Contains("Player disconnect"))
			{
				display = false;
				foreach (IEventPlayerDisconnect Event in server.playerdisconnect)
				{
					string name = stream.Substring(stream.IndexOf(":"));
					Event.OnPlayerDisconnect(name);
				}
			}

			if (stream.Contains("Player has connected before load is complete"))
			{
				if (server.ServerModCheck(1, 5, 0))
				{
					server.fixBuggedPlayers = true;
				}
			}

			if (display)
			{
				server.write(stream.Trim(), color);

				// Limiting output speed for generic message
				Thread.Sleep(server.printSpeed);
			}
		}
	}
}
