using System;
using System.Threading;

namespace MegaAdmin
{
	class WindowResizeWatcherThread
	{
		private int lastw = Console.WindowWidth;
		private int lasth = Console.WindowHeight;
		public WindowResizeWatcherThread()
		{
			while (true)
			{
				Thread.Sleep(100);
				if (lastw != Console.WindowWidth || lasth != Console.WindowHeight)
				{
					lastw = Console.WindowWidth;
					lasth = Console.WindowHeight;
					Console.Clear();
					Program.WriteBuffer(Program.servers[Program.selected]);
					Program.WriteMenu();
					Program.buffclear = string.Empty;
					for (ushort x = 0; x < Console.WindowTop + Console.WindowHeight - 3; x++)
					{
						for (ushort i = 0; i < Console.WindowWidth - 1; i++)
						{
							Program.buffclear = Program.buffclear + " ";
						}
						Program.buffclear = Program.buffclear + Environment.NewLine;
					}
				}
			}
		}
	}
}
