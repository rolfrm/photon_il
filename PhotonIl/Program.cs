using System;
using Gtk;

namespace PhotonIl
{
	class MainClass
	{
		public static void Main (string[] args)
		{
            IlGenTest.Test3();
            return;
            IlGenTest.Test1();
            IlGenTest.Test2();
            return;
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
