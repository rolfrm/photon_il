using System;
using PhotonIl;
using System.Linq;


namespace SimpleCli
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			
			Console.Clear ();
			Console.WriteLine ("Welcome to Photon for .NET.");
			Console.Write (">");
			var gen = new IlGen ();
			var fcn = gen.DefineFunction ("testfn", gen.VoidType);
			var cb = new CodeBuilder (gen);
			cb.PushArgument ();
			Console.CursorVisible = true;
			Console.TreatControlCAsInput = true;
			while (true) {
				var keyinfo = Console.ReadKey ();
				if (keyinfo.Modifiers == 0 && keyinfo.Key != ConsoleKey.Enter && keyinfo.Key != ConsoleKey.Backspace) {
					var str = cb.GetString ();
					str += keyinfo.KeyChar;
					cb.SetString (str);
				} else if (keyinfo.Key == ConsoleKey.Enter) {
					cb.SelectOption(cb.GetOptions ().FirstOrDefault ());
					cb.PushArgument ();
					
				} else if (keyinfo.Modifiers == ConsoleModifiers.Control) {
					var key = keyinfo.Key;
					if (key == ConsoleKey.N) {
						Console.WriteLine ("Lets try it!");
						try{
							cb.SelectOption(cb.GetOptions ().FirstOrDefault ());
							cb.BuildAndRun();
							return;
						}catch(Exception e){
							Console.WriteLine ("!!! {0}", e.Message);
							return;
						}
						
					}
				}
				Console.Clear ();
				Console.CursorLeft = 0;
				int index = 0;
				var subs = gen.SubExpressions.Get (cb.SelectedExpression);
				for (int i = 0; i < subs.Count; i++) {
					if (i != 0) {
						Console.Write ("\t");
					}
					if (i == cb.SelectedIndex) {
						var currentstring = cb.GetString ();
						index = Console.CursorLeft;
						Console.Write (currentstring);
						continue;
					}
					Console.Write (subs [i].ToString());
				}
				Console.CursorLeft = index;
			}

		}
	}
}
