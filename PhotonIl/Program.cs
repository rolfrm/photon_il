using System;
using Gtk;

namespace PhotonIl
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			IlGenTest.Run2 ();
            return;
			var gen = IlGenTest.Run ();
			//eturn;
			Application.Init ();
			MainWindow win = new MainWindow ();
			win.LoadIlGen (gen);

			win.Show ();
			Application.Run ();
		}
	}
}
