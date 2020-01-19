using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace MegaAdmin
{
	class OutputThread
	{
		private static readonly Regex SmodRegex = new Regex(@"\[(DEBUG|INFO|WARN|ERROR)\] (\[.*?\]) (.*)", RegexOptions.Compiled | RegexOptions.Singleline);
		private bool fixBuggedPlayers;
		private Server server;

		public OutputThread(Server server)
		{
			this.server = server;
			server.write("Output Thread started...",Color.White);
			while (!Directory.Exists("SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + server.SID))
			{
				//wait for directory to be created...
			}
			FileSystemWatcher watcher = new FileSystemWatcher("SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + server.SID, "sl*.mapi");
			watcher.IncludeSubdirectories = false;
			watcher.Created += OnMapiCreated;
			watcher.EnableRaisingEvents = true;
		}

		private bool ServerModCheck(int major, int minor, int fix)
		{
			if (server.ServerModVersion == null)
			{
				return false;
			}

			string[] parts = server.ServerModVersion.Split('.');
			int verMajor = 0;
			int verMinor = 0;
			int verFix = 0;
			if (parts.Length == 3)
			{
				Int32.TryParse(parts[0], out verMajor);
				Int32.TryParse(parts[1], out verMinor);
				Int32.TryParse(parts[2], out verFix);
			}
			else if (parts.Length == 2)
			{
				Int32.TryParse(parts[0], out verMajor);
				Int32.TryParse(parts[1], out verMinor);
			}
			else
			{
				return false;
			}

			if (major == 0 && minor == 0 && verFix == 0)
			{
				return false;
			}

			return (verMajor > major) || (verMajor >= major && verMinor > minor) || (verMajor >= major && verMinor >= minor && verFix >= fix);

		}

		private void OnMapiCreated(object sender, FileSystemEventArgs e)
		{
			if (!File.Exists(e.FullPath)) return;

			try
			{
				ProcessFile(e.FullPath);
			}
			catch (Exception ex)
			{
				server.write(ex.Message, Color.Red);
			}
		}

		private void ProcessFile(string file)
		{
			string stream = string.Empty;
			string command = "open";
			bool isRead = false;

			// Lock this object to wait for this event to finish before trying to read another file
			lock (this)
			{
				for (int attempts = 0; attempts < 100; attempts++)
				{
					try
					{
						if (!File.Exists(file)) return;

						// Lock the file to prevent it from being modified further, or read by another instance
						using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)))
						{
							command = "read";
							stream = sr.ReadToEnd();

							isRead = true;
						}

						command = "delete";
						File.Delete(file);

						break;
					}
					catch (UnauthorizedAccessException e)
					{
						server.write(e.Message, Color.Red);
						Thread.Sleep(8);
					}
					catch (Exception e)
					{
						server.write(e.Message, Color.Red);
						Thread.Sleep(5);
					}
				}
			}

			if (!isRead)
			{
				server.write($"Message printer warning: Could not {command} \"{file}\". Make sure that {nameof(MegaAdmin)} has all necessary read-write permissions\nSkipping...",Color.Red);

				return;
			}

			string color = Color.Cyan;
			bool display = true;

			if (stream.EndsWith(Environment.NewLine))
				stream = stream.Substring(0, stream.Length - Environment.NewLine.Length);

			// Smod2 loggers pretty printing
			Match match = SmodRegex.Match(stream);
			if (match.Success)
			{
				if (match.Groups.Count >= 3)
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
							break;
					}

					server.writePart(string.Empty, Color.Cyan, true, false);
					server.writePart("[" + match.Groups[1].Value + "] ", levelColor, false, false);
					server.writePart(match.Groups[2].Value + " ", tagColor, false, false);
					server.writePart(match.Groups[3].Value, msgColor, false, true);

					// P.S. the format is [Info] [courtney.exampleplugin] Something interesting happened
					// That was just an example

					// This return should be here
					return;
				}
			}
			if (stream.Contains("Server starting at all IPv4 addresses and port"))
			{
				string str = stream.Replace("Server starting at all IPv4 addresses and port ", string.Empty);
				ushort port = 0;
				if (ushort.TryParse(str.Trim(), out port))
				{
					server.Port = port;
				}
			}
			else if (stream.Contains("Mod Log:"))
			{
				foreach (IEventAdminAction Event in server.adminaction)
				{
					Event.OnAdminAction(stream.Replace("Mod log:", string.Empty));
				}
			}
			else if (stream.Contains("ServerMod - Version"))
			{
				string[] streamSplit;
				server.HasServerMod = true;
				// This should work fine with older ServerMod versions too
				streamSplit = stream.Replace("ServerMod - Version", string.Empty).Split('-');
				server.ServerModVersion = streamSplit[0].Trim();
				server.ServerModBuild = (streamSplit.Length > 1 ? streamSplit[1] : "A").Trim();
			}
			else if (stream.Contains("Round restarting"))
			{
				foreach (IEventRoundEnd Event in server.roundend)
				{
					Event.OnRoundEnd();
				}
			}
			else if (stream.Contains("Waiting for players"))
			{
				if (!server.InitialRoundStarted)
				{
					server.InitialRoundStarted = true;
					foreach (IEventRoundStart Event in server.roundstart)
					{
						Event.OnRoundStart();
					}
				}
				if (fixBuggedPlayers)
				{
					server.SendMessage("ROUNDRESTART");
					fixBuggedPlayers = false;
				}
			}
			else if (stream.Contains("New round has been started"))
			{
				foreach (IEventRoundStart Event in server.roundstart)
				{
					Event.OnRoundStart();
				}
			}
			else if (stream.Contains("Level loaded. Creating match..."))
			{
				foreach (IEventServerStart Event in server.serverstart)
				{
					Event.OnServerStart();
				}
			}
			else if (stream.Contains("Server full"))
			{
				foreach (IEventServerFull Event in server.serverfull)
				{
					Event.OnServerFull();
				}
			}
			else if (stream.Contains("Player connect"))
			{
				display = false;
				foreach (IEventPlayerConnect Event in server.playerconnect)
				{
					string name = stream.Substring(stream.IndexOf(":"));
					Event.OnPlayerConnect(name);
				}
			}
			else if (stream.Contains("Player disconnect"))
			{
				display = false;
				foreach (IEventPlayerDisconnect Event in server.playerdisconnect)
				{
					string name = stream.Substring(stream.IndexOf(":"));
					Event.OnPlayerDisconnect(name);
				}
			}
			else if (stream.Contains("Player has connected before load is complete"))
			{
				fixBuggedPlayers = true;
			}
			if (display)
			{
				server.write(stream, color);
				Thread.Sleep(server.printSpeed);
			}
		}
	}
}
