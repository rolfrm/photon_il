using System;
using System.IO;
using PhotonIl;
using System.Linq;


namespace SimpleCli
{
	public class MainClass
	{
		
		public static void exit(){
			System.Environment.Exit (0);
		}

		public static void save(string filepath){

		}

		public static void load(string filepath){

		}

		public static void Main (string[] args)
		{
			Console.CursorVisible = true;
			Console.TreatControlCAsInput = true;

			Console.WriteLine ("Welcome to Photon for .NET.");

			var gen = new IlGen (true);
			gen.LoadReference ("baseworkspace.bin");
			File.Delete ("SimpleCli.bin");
			if (!File.Exists ("SimpleCli.bin")) {
				var gen2 = new IlGen ();
				gen2.LoadReference ("baseworkspace.bin");
				gen2.AddFunctionInvocation(typeof(MainClass), nameof(exit));
				gen2.AddFunctionInvocation(typeof(MainClass), nameof(save));
				gen2.AddFunctionInvocation(typeof(MainClass), nameof(load));
				gen2.Save ("SimpleCli.bin");
			}
			gen.LoadReference ("SimpleCli.bin");


			var cb = new CodeBuilder (gen);
			cb.PushArgument ();

			while (true) {
				var keyinfo = Console.ReadKey ();
				var key = keyinfo.Key;
				var mod = keyinfo.Modifiers;

				if ((key == ConsoleKey.LeftArrow || key == ConsoleKey.RightArrow)
				    && keyinfo.Modifiers == ConsoleModifiers.Shift) {
					if (cb.SelectedIndex >= 0 && cb.CurrentExpression == Uid.Default)
						cb.SelectCurrentOption ();
					int direction = key == ConsoleKey.LeftArrow ? -1 : 1;
					if (cb.SelectedIndex + direction == cb.NArguments)
						cb.PushArgument ();
					else
						cb.SelectedIndex += direction;
					
				} else if (keyinfo.Key == ConsoleKey.Enter) {
					if (mod == ConsoleModifiers.Alt)
						cb.CreateSub ();
					if(!gen.SubExpressions.Contains(cb.CurrentExpression)){
						cb.SelectCurrentOption ();
					}
					else{
						cb.Enter ();
					}
				} else if (key == ConsoleKey.Escape) {
					if (cb.SelectedIndex >= 0 && cb.CurrentExpression == Uid.Default)
						cb.SelectCurrentOption ();
					cb.Exit ();
					if (cb.SelectedExpression == Uid.Default) {
						cb = new CodeBuilder (gen);
					}
				} else if (key == ConsoleKey.Delete) {
					cb.Delete ();
				
				} else if (key == ConsoleKey.Backspace) {
					var str = cb.GetString ();
					if (str.Length > 0) {
						str = str.Remove (str.Length - 1);
						cb.SetString (str);
					}
				} else if (keyinfo.Modifiers == ConsoleModifiers.Control) {
					if (key == ConsoleKey.C) {
						cb.CleanSelectedExpression ();
					}
					if (key == ConsoleKey.N) {
						Console.WriteLine ("");
						try {
							cb.SelectCurrentOption ();
							cb.BuildAndRun ();
							Console.WriteLine ("");
						} catch (Exception e) {
							Console.WriteLine ("Error: {0}", e.Message);
							continue;
						}
					}

				} else if (mod == ConsoleModifiers.Alt) {
					if (key == ConsoleKey.K || key == ConsoleKey.I) {
						cb.OptionIndex += key == ConsoleKey.K ? -1 : 1;
					}

				}
				else if ((mod == 0 || mod == ConsoleModifiers.Shift) && keyinfo.KeyChar != 0) {
					var str = cb.GetString ();
					str += keyinfo.KeyChar;
					cb.SetString (str);
				} else
					continue;
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
						Console.Write (currentstring);
						index = Console.CursorLeft;
						continue;
					}
					Console.Write (cb.StringOf(subs [i]));
				}

				Console.BackgroundColor = ConsoleColor.DarkGray;

				Console.CursorLeft = index + 1;
				var options = cb.GetOptions ();
				if (options.Length > 0) {
					var idx = cb.OptionIndex;
						
				    
					var opt = Math.Abs (idx) % options.Length;
					var option = options [opt];
					Console.Write (cb.StringOf(option));

				}else if(cb.CurrentExpression != Uid.Default){
					//Console.Write (cb.StringOf(cb.CurrentExpression));
				}
				Console.ResetColor ();
				Console.CursorLeft = index;
			}
		}
	}
}
