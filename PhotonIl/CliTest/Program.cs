using System;
using System.Diagnostics;

namespace CliTest
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.TreatControlCAsInput = true;
			//Process.Start ("stty", "-echo -icanon");

			while (true) {
				var kinfo = Console.ReadKey ();
				Console.CursorLeft = 0;
				Console.WriteLine ("{0} {1}", kinfo.Key, kinfo.Modifiers);
			}
		}
	}
}
