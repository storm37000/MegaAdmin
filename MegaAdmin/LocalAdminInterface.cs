using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MegaAdmin
{
	class LocalAdminInterface
	{
		private readonly Regex SMOD_REGEX = new Regex(@"\[(DEBUG|INFO|WARN|ERROR)\] (\[.*?\]) (.*)", RegexOptions.Compiled | RegexOptions.Singleline);
		public readonly Process process = new Process();
		public bool started { get; private set; } = false;
		private Server server;
		public LocalAdminInterface(Server server)
		{
			this.server = server;
			string binary;
			if(Program.RunningPlatform() == Program.Platform.Windows)
			{
				binary = "LocalAdmin.exe";
			}
			else
			{
				binary = "LocalAdmin";
			}
			process.StartInfo.FileName = binary;
			process.StartInfo.RedirectStandardInput = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.UseShellExecute = false;
			process.EnableRaisingEvents = true;
			process.OutputDataReceived += read;
			process.Exited += (sender, eventArgs) => { server.write("Server stopped",Color.Yellow); started = false; };
		}

		public void start()
		{
			server.write("Server Starting...",Color.Yellow);
			try
			{
				process.Start();
				started = true;
				server.write("Server Started", Color.Green);
			}
			catch(Exception e)
			{
				server.write("Error starting " + process.StartInfo.FileName + " - " + e.Message,Color.Red);
			}
		}
		private void read(object sender, DataReceivedEventArgs e)
		{
			string stream = e.Data;
			string[] streamSplit;
			string color = Color.Cyan;
			bool display = true;

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

					// Limiting output speed for Smod messages
					//Thread.Sleep(server.printSpeed);

					// This return should be here
					return;
				}
			}
			if (stream.Contains("Server starting at all IPv4 addresses and port"))
			{
				string str = stream.Replace("Server starting at all IPv4 addresses and port ", string.Empty);
				server.Port = ushort.Parse(str.Trim());
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
				server.HasServerMod = true;
				// This should work fine with older ServerMod versions too
				streamSplit = stream.Replace("ServerMod - Version", string.Empty).Split('-');
				server.ServerModVersion = streamSplit[0].Trim();
				server.ServerModBuild = (streamSplit.Length > 1 ? streamSplit[1] : "A").Trim();
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
				if (server.ServerModCheck(1, 5, 0))
				{
					server.fixBuggedPlayers = true;
				}
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
			if (display)
			{
				server.write(stream, color);
			}
		}
		public void write(string msg)
		{
			if (started)
			{
				process.StandardInput.WriteLine(msg);
			}
		}
	}
}
