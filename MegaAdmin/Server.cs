using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace MegaAdmin
{
	public class Server
	{
		public readonly EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
		private bool signaled = false;
		public readonly EventWaitHandle CMDevent = new EventWaitHandle(false, EventResetMode.AutoReset);
		private uint logID;
		public string SID { get; private set; } = string.Empty;
		public ushort Port { get; set; }
		public List<string> buffer { get; private set; } = new List<string>();
		public string cmdbuffer = string.Empty;
		public bool cmdlock { get; private set; } = false;
		public string Name
		{
			get
			{
				if (Port != 0)
				{
					return "Port:      " + Port;
				}
				else if (SID != string.Empty)
				{
					return SID;
				}
				else
				{
					return " Starting... ";
				}
			}
		}
		public string AppFolder { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "SCP Secret Laboratory" + Path.DirectorySeparatorChar;
		public YamlConfig Config { get; private set; }

		// set via config files
		public bool nolog { get; private set; }
		// 

		public string LogFolder
		{
			get
			{
				string loc = AppFolder + "ServerLogs" + Path.DirectorySeparatorChar + Port + Path.DirectorySeparatorChar;
				if (!Directory.Exists(loc))
				{
					Directory.CreateDirectory(loc);
				}
				return loc;
			}
		}
		public bool HasServerMod { get; set; }
		public string ServerModVersion { get; set; }
		public string ServerModBuild { get; set; }

		public bool InitialRoundStarted { get; set; }
		public bool runOptimized { get; private set; } = true;
		public int printSpeed { get; private set; } = 150;
		public bool stopping { get; private set; } = false;
		public bool restarting { get; private set; } = false;

		//private Thread printerThread;
		public Process GameProcess { get; private set; }

		public List<Feature> features = new List<Feature>();

		//BEGIN EVENT LIST
		public List<IEventRoundEnd> roundend = new List<IEventRoundEnd>();
		public List<IEventRoundStart> roundstart = new List<IEventRoundStart>();
		public List<IEventAdminAction> adminaction = new List<IEventAdminAction>();
		public List<IEventServerStart> serverstart = new List<IEventServerStart>();
		public List<IEventServerPreStart> preserverstart = new List<IEventServerPreStart>();
		public List<IEventServerFull> serverfull = new List<IEventServerFull>();
		public List<IEventPlayerConnect> playerconnect = new List<IEventPlayerConnect>();
		public List<IEventPlayerDisconnect> playerdisconnect = new List<IEventPlayerDisconnect>();
		public List<IEventConfigReload> configreload = new List<IEventConfigReload>();
		//END EVENT LIST

		private static readonly Regex SmodRegex = new Regex(@"\[(DEBUG|INFO|WARN|ERROR)\] (\[.*?\]) (.*)", RegexOptions.Compiled | RegexOptions.Singleline);
		private bool fixBuggedPlayers;

		public Server()
		{
			cmdlock = true;
			Program.servers.Add(this);
			Program.WriteMenu(this);
			new Feature_Loader("MeA_features",this);
			Reload();
			//printerThread = new Thread(new ThreadStart(() => new OutputThread(this)));
			//printerThread.Name = "OutputThread";
			Start();
			// Wait if someone tells us to die or do every five seconds something else.
			do
			{
				signaled = waitHandle.WaitOne(10);
				if (CMDevent.WaitOne(10))
				{
					cmd();
				}
			} while (!signaled);
		}

//		public void OnTick()
//		{
//			if (GameProcess != null && !GameProcess.HasExited)
//			{
//			}
//			else if (!stopping)
//			{
//				foreach (IEventCrash f in cras)
//				{
//					if (f is IEventCrash)
//					{
//						((IEventCrash)f).OnCrash();
//					}
//				}
//
//				Write("Game engine exited/crashed/closed/restarting", ConsoleColor.Red);
//				Write("Cleaning Session", ConsoleColor.Red);
//				CleanUp();
//				GenerateSessionID();
//				Write("Restarting game with new session id");
//				StartServer();
//				InitFeatures();
//			}
//		}

		private void Start()
		{
			stopping = false;
			restarting = false;
			SID = Program.GenerateSessionID();
			this.write("starting server with session ID: " + SID, Color.Yellow);
			InitialRoundStarted = false;
			string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "SCPSL.*", SearchOption.TopDirectoryOnly);
			if (files.Length == 0)
			{
				write("Failed - couldnt find server executable file!", Color.Red);
				write("Please run the 'restart' command to try again...", Color.Gray);
				cmdlock = false;
				Program.WriteInput(this);
				return;
			}
			string args;
			if (nolog) //nolog
			{
				if (Program.RunningPlatform() != Program.Platform.Windows)
				{
					args = "-batchmode -nographics -nolog -key" + SID + " -silent-crashes -nodedicateddelete -id" + (object)Process.GetCurrentProcess().Id + " -logFile /dev/null";
				}
				else
				{
					args = "-batchmode -nographics -nolog -key" + SID + " -silent-crashes -nodedicateddelete -id" + (object)Process.GetCurrentProcess().Id + " -logFile NUL";
				}
			}
			else
			{
				args = "-batchmode -nographics -key" + SID + " -silent-crashes -nodedicateddelete -id" + (object)Process.GetCurrentProcess().Id + " -logFile \"" + LogFolder + "SCP_output_log.txt" + "\"";
			}
			write("Starting server with the following parameters", Color.Yellow);
			try
			{
				write(files[0] + " " + args, Color.Yellow);
				ProcessStartInfo startInfo = new ProcessStartInfo(files[0]);
				startInfo.Arguments = args;
				GameProcess = Process.Start(startInfo);
			}
			catch (Exception e)
			{
				write("Failed - Executable file or config issue!", Color.Red);
				write(e.Message, Color.Red);
				write("Please run the 'restart' command to try again...", Color.Gray);
				cmdlock = false;
				Program.WriteInput(this);
				return;
			}
			foreach (IEventServerPreStart Event in preserverstart)
			{
				Event.OnServerPreStart();
			}
			while (!Directory.Exists("SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID))
			{
				//wait for directory to be created...
			}
			FileSystemWatcher watcher = new FileSystemWatcher("SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID, "sl*.mapi");
			watcher.IncludeSubdirectories = false;
			watcher.Created += OnMapiCreated;
			watcher.EnableRaisingEvents = true;
			cmdlock = false;
			Program.WriteInput(this);
		}

		public void Stop()
		{
			if (GameProcess != null)
			{
				write("Stopping Server...", Color.Yellow);
				SendMessage("quit");
				while (!GameProcess.HasExited){}
				DeleteSession();
				write("Stopped Server", Color.Green);
			}
			if (!restarting)
			{
				Program.stopServer(this);
			}
		}

		public void SoftRestart()
		{
			restarting = true;
			SendMessage("RECONNECTRS");
			Stop();
			Start();
			Program.WriteMenu(this);
		}

		private void CleanSession()
		{
			string path = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			if (Directory.Exists(path))
			{
				foreach (string file in Directory.GetFiles(path))
				{
					File.Delete(file);
				}
			}

		}

		private void DeleteSession()
		{
			if (SID == string.Empty) { return; }
			CleanSession();
			string path = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			if (Directory.Exists(path)) Directory.Delete(path);
		}

		public void write(string msg, string color)
		{
			lock (this)
			{
				string[] msgspl = Timestamp(msg).Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
				foreach (string mess in msgspl)
				{
					if (mess == Environment.NewLine) { continue; }
					else
					{
						buffer.Add(color + mess.Substring(0, Program.ClampI(Console.BufferWidth - 1, 0, mess.Length)) + Color.Reset + Environment.NewLine);
						Program.WriteBuffer(this);
					}
				}
				Log(msg);
			}
		}

		public void writePart(string msg, string color, bool date = false, bool lineEnd = false)
		{
			lock (this)
			{
				string msgcol = color + msg + Color.Reset;
				if (date)
				{
					buffer.Add(Timestamp(msgcol));
					Program.WriteBuffer(this);
				}
				else if (lineEnd && !msg.EndsWith(Environment.NewLine))
				{
					buffer.Add(msgcol + Environment.NewLine);
					Program.WriteBuffer(this);
				}
				else
				{
					buffer.Add(msgcol);
					Program.WriteBuffer(this);
				}
				Log(msg, lineEnd);
			}
		}

		public void cmd()
		{
			cmdlock = true;
			Program.WriteInput(this);
			bool send = true;
			string command = cmdbuffer;
			this.write(">>> " + command, Color.DarkMagenta);
			if (command.Length >= 3)
			{
				if (command.ToLower() == "config reload")
				{
					this.write("Reloading Config...", Color.Blue);
					Reload();
					this.write("Config Reloaded!", Color.Green);
				}
				else if (command.ToLower() == "quit")
				{
					stopping = true;
					Stop();
					send = false;
				}
				else if (command.ToLower() == "restart")
				{
					SoftRestart();
					send = false;
				}
				else if (command.ToLower().Substring(0, 3) == "new")
				{
					Program.startServer();
					send = false;
				}
			}
			if (send)
			{
				SendMessage(command);
			}
			cmdlock = false;
			CMDevent.Reset();
			cmdbuffer = string.Empty;
			Program.WriteInput(this);
		}

		private static string logbuff = ""; //buffer log output until we know the servers port if multimode isnt enabled.

		private void Log(string message, bool newline = true)
		{
			if (!nolog)
			{
				message = Timestamp(message);
				if (Port == 0)
				{
					if (newline)
					{
						logbuff = logbuff + message + Environment.NewLine;
					}
					else
					{
						logbuff = logbuff + message;
					}
				}
				else
				{
					using (StreamWriter sw = File.AppendText(LogFolder + "MeA_output_log.txt"))
					{
						if (logbuff != "")
						{
							sw.Write(logbuff);
							logbuff = "";
						}
						if (newline)
						{
							sw.WriteLine(message);
						}
						else
						{
							sw.Write(message);
						}
					}
				}
			}
		}

		public void SendMessage(string message)
		{
			string sessionDirectory = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			if (!Directory.Exists(sessionDirectory))
			{
				this.write("Send Message error: sending " + message + " failed. " + sessionDirectory + " does not exist!", Color.Yellow);
				this.write("skipping", Color.Yellow);
				return;
			}
			string file = sessionDirectory + Path.DirectorySeparatorChar + "cs" + logID + ".mapi";
			if (File.Exists(file))
			{
				this.write("Send Message error: sending " + message + " failed. " + file + " already exists!", Color.Yellow);
				this.write("skipping", Color.Yellow);
				logID++;
				return;
			}
			this.write("Sending request to SCP: Secret Laboratory...", Color.Yellow);
			StreamWriter streamWriter = new StreamWriter(file);
			logID++;
			streamWriter.WriteLine(message + "terminator");
			streamWriter.Close();
		}

		public static string Timestamp(string message)
		{
			if (string.IsNullOrEmpty(message))
				return string.Empty;
			DateTime now = DateTime.Now;
			message = "[" + now.Hour.ToString("00") + ":" + now.Minute.ToString("00") + ":" + now.Second.ToString("00") + "] " + message;
			return message;
		}

		public void Reload()
		{
			Config = new YamlConfig("MeA_config.yaml");
			nolog = Config.GetBool("nolog",true);
			printSpeed = Config.GetInt("print_speed");
		}

		private bool ServerModCheck(int major, int minor, int fix)
		{
			if (ServerModVersion == null)
			{
				return false;
			}

			string[] parts = ServerModVersion.Split('.');
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
				write(ex.Message, Color.Red);
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
						write(e.Message, Color.Red);
						Thread.Sleep(8);
					}
					catch (Exception e)
					{
						write(e.Message, Color.Red);
						Thread.Sleep(5);
					}
				}
			}

			if (!isRead)
			{
				write($"Message printer warning: Could not {command} \"{file}\". Make sure that {nameof(MegaAdmin)} has all necessary read-write permissions\nSkipping...", Color.Red);

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

					writePart(string.Empty, Color.Cyan, true, false);
					writePart("[" + match.Groups[1].Value + "] ", levelColor, false, false);
					writePart(match.Groups[2].Value + " ", tagColor, false, false);
					writePart(match.Groups[3].Value, msgColor, false, true);

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
					Port = port;
				}
			}
			else if (stream.Contains("Mod Log:"))
			{
				foreach (IEventAdminAction Event in adminaction)
				{
					Event.OnAdminAction(stream.Replace("Mod log:", string.Empty));
				}
			}
			else if (stream.Contains("ServerMod - Version"))
			{
				HasServerMod = true;
				// This should work fine with older ServerMod versions too
				string[] streamSplit = stream.Replace("ServerMod - Version", string.Empty).Split('-');

				if (streamSplit.Length != 0)
				{
					ServerModVersion = streamSplit[0].Trim();
					ServerModBuild = (streamSplit.Length > 1 ? streamSplit[1] : "A").Trim();
				}
			}
			else if (stream.Contains("Round restarting"))
			{
				foreach (IEventRoundEnd Event in roundend)
				{
					Event.OnRoundEnd();
				}
			}
			else if (stream.Contains("Waiting for players"))
			{
				if (!InitialRoundStarted)
				{
					InitialRoundStarted = true;
					foreach (IEventRoundStart Event in roundstart)
					{
						Event.OnRoundStart();
					}
				}
				if (fixBuggedPlayers)
				{
					SendMessage("ROUNDRESTART");
					fixBuggedPlayers = false;
				}
			}
			else if (stream.Contains("New round has been started"))
			{
				foreach (IEventRoundStart Event in roundstart)
				{
					Event.OnRoundStart();
				}
			}
			else if (stream.Contains("Level loaded. Creating match..."))
			{
				foreach (IEventServerStart Event in serverstart)
				{
					Event.OnServerStart();
				}
			}
			else if (stream.Contains("Server full"))
			{
				foreach (IEventServerFull Event in serverfull)
				{
					Event.OnServerFull();
				}
			}
			else if (stream.Contains("Player connect"))
			{
				display = false;
				foreach (IEventPlayerConnect Event in playerconnect)
				{
					string name = stream.Substring(stream.IndexOf(":"));
					Event.OnPlayerConnect(name);
				}
			}
			else if (stream.Contains("Player disconnect"))
			{
				display = false;
				foreach (IEventPlayerDisconnect Event in playerdisconnect)
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
				write(stream, color);
				Thread.Sleep(printSpeed);
			}
		}
	}
}
