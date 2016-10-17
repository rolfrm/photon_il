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
			var cb = new CodeBuilder (gen);
			cb.PushArgument ();
			Console.CursorVisible = true;
			Console.TreatControlCAsInput = true;
			while (true) {
				var keyinfo = Console.ReadKey ();
				var key = keyinfo.Key;
				var mod = keyinfo.Modifiers;

				//Console.WriteLine ("{0} {1}", key, mod);
				//continue;
				if ((key == ConsoleKey.LeftArrow || key == ConsoleKey.RightArrow)
				    && keyinfo.Modifiers == ConsoleModifiers.Shift) {
					if (cb.SelectedIndex >= 0 && cb.CurrentExpression == Uid.Default)
						cb.SelectOption (cb.GetOptions ().FirstOrDefault ());
					int direction = key == ConsoleKey.LeftArrow ? -1 : 1;
					if (cb.SelectedIndex + direction == cb.NArguments)
						cb.PushArgument ();
					else
						cb.SelectedIndex += direction;
					
				} else if (keyinfo.Key == ConsoleKey.Enter) {
					if (mod == ConsoleModifiers.Alt)
						cb.CreateSub ();
					cb.Enter ();
				} else if (key == ConsoleKey.Escape) {
					if (cb.SelectedIndex >= 0 && cb.CurrentExpression == Uid.Default)
						cb.SelectOption (cb.GetOptions ().FirstOrDefault ());
					cb.Exit ();
				} else if (key == ConsoleKey.Delete) {
					cb.Delete ();
				
				}else if(key == ConsoleKey.Backspace){
					var str = cb.GetString ();
					if (str.Length > 0) {
						str = str.Remove (str.Length - 1);
						cb.SetString (str);
					}
				} else if (keyinfo.Modifiers == ConsoleModifiers.Control) {
					if (key == ConsoleKey.N) {
						Console.WriteLine ("");
						Console.Write (">>");
						try {
							cb.SelectOption (cb.GetOptions ().FirstOrDefault ());
						    cb.BuildAndRun ();
							Console.WriteLine ("");
						} catch (Exception e) {
							Console.WriteLine ("Error: {0}", e.Message);
							return;
						}
					}
				} else if (mod == 0) {
					var str = cb.GetString ();
					str += keyinfo.KeyChar;
					cb.SetString (str);
				}
				Console.CursorLeft = 0;
				Console.Write (new String (' ', Console.WindowWidth - 1));
				Console.CursorLeft = 0;
				int index = 0;
				var subs = gen.SubExpressions.Get (cb.SelectedExpression);
				if (cb.SelectedIndex == -1)
					Console.Write ("|");
				for (int i = 0; i < subs.Count; i++) {
					if (i != 0)
						Console.Write ("\t");
					
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
