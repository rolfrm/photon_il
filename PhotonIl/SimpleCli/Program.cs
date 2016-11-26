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
			gen.Save (filepath);
		}

		public static void load(string filepath){
			gen.Load (filepath);
		}

		public static void load_edit(string filepath){
			gen.Load (filepath, import: true);
		}

		// starts editing a specfic funcion
		public static void print_functions(string funcname){
			foreach (var x in gen.FunctionName)
				if (x.Value == funcname)
					Console.WriteLine (x.Key);
		}

		public static Uid get_uid(string funcname, int index){
			foreach (var x in gen.FunctionName)
				if (x.Value == funcname && index-- == 0)
					return x.Key;
			return Uid.Default;

		}

		public static void edit(Uid func){
			var defun = gen.MacroNames.First (x => x.Value == "defun").Key;
			Uid body = gen.GetFunctionBody (func);
			Uid def = Uid.Default;
			foreach (var subexpr in gen.SubExpressions.Entries) {
				if (subexpr.Value.Count != 4 || subexpr.Value[0] != defun || subexpr.Value[3] != body)
					continue;
				def = subexpr.Key;
				break;
			}
			def = gen.functionBody.First(x => x.Value == def).Key;
			if (def == Uid.Default)
				throw new Exception (string.Format("Cannot find function definition for {0}", func));
			cb = new CodeBuilder (gen, def);
		}

		public static Uid sym1(string name){
			return gen.Sym (name);
		}

		public static Uid sym2(){
			return Uid.CreateNew ();
		}

		static IlGen gen;
		static CodeBuilder cb;

		public static void Main (string[] args)
		{	
			Uid errorExpr = Uid.Default;
			Console.CursorVisible = true;
			Console.TreatControlCAsInput = true;

			Console.WriteLine ("Welcome to Photon for .NET.");

			gen = new IlGen (true);
			gen.LoadReference ("baseworkspace.bin");
			File.Delete ("SimpleCli.bin");
			if (!File.Exists ("SimpleCli.bin")) {
				var gen2 = new IlGen (true);
				gen2.LoadReference ("baseworkspace.bin");
				gen2.AddFunctionInvocation(typeof(MainClass), nameof(exit));
				gen2.AddFunctionInvocation(typeof(MainClass), nameof(save));
				gen2.AddFunctionInvocation(typeof(MainClass), nameof(load));
				gen2.AddFunctionInvocation (typeof(MainClass), nameof (load_edit), "load edit");
				gen2.AddFunctionInvocation (typeof(MainClass), nameof (edit));
				gen2.AddFunctionInvocation (typeof(MainClass), nameof (print_functions), "print functions");
				gen2.AddFunctionInvocation (typeof(MainClass), nameof (get_uid), "get uid");
				gen2.AddFunctionInvocation (typeof(MainClass), nameof (sym1), "sym");
				gen2.AddFunctionInvocation (typeof(MainClass), nameof (sym2), "sym");

				gen2.Save ("SimpleCli.bin");
			}
			gen.LoadReference ("SimpleCli.bin");

			cb = new CodeBuilder (gen);
			cb.PushArgument ();

			while (true) {
				var keyinfo = Console.ReadKey ();
				var key = keyinfo.Key;
				var mod = keyinfo.Modifiers;
				//Console.WriteLine ("{0} {1}", mod, key);
				//continue;
				if ((key == ConsoleKey.LeftArrow || key == ConsoleKey.RightArrow)
				    && mod == ConsoleModifiers.Shift) {
					if (cb.SelectedIndex >= 0 && cb.CurrentExpression == Uid.Default)
						cb.SelectCurrentOption ();
					int direction = key == ConsoleKey.LeftArrow ? -1 : 1;
					if (cb.SelectedIndex + direction == cb.NArguments)
						cb.PushArgument ();
					else
						cb.SelectedIndex += direction;
					
				} else if (keyinfo.Key == ConsoleKey.Enter) {
					if (cb.GetOptions().Length == 0) {
						cb.CreateSub ();
						cb.Enter ();
					}
					if (!gen.SubExpressions.Contains (cb.CurrentExpression)) {
						cb.SelectCurrentOption ();
						if (gen.SubExpressions.Contains (cb.CurrentExpression))
							cb.Enter ();
						cb.PushArgument ();
					} else {
						cb.Enter ();
					}
				} else if (key == ConsoleKey.Escape) {
					if (cb.SelectedIndex >= 0 && cb.CurrentExpression == Uid.Default)
						cb.SelectCurrentOption ();
					cb.Exit ();
                    
                    if (cb.SelectedExpression == Uid.Default) {
						cb = new CodeBuilder (gen);
						cb.PushArgument ();
                    }
                    else
                    {
                        errorExpr = cb.TryCompile()?.Expr ?? Uid.Default;
                    }

				} else if (key == ConsoleKey.Delete) {
					cb.Delete ();
				
				} else if (key == ConsoleKey.Backspace) {
					var str = cb.GetString ();
					if (str.Length > 0) {
						str = str.Remove (str.Length - 1);
						cb.SetString (str);
					}
				} else if (mod == ConsoleModifiers.Control && key == ConsoleKey.C) {
                    cb.CleanSelectedExpression();
                }
                else if (mod == ConsoleModifiers.Control && key == ConsoleKey.N)
                {
                    Console.WriteLine("");
                    try
                    {
                        cb.SelectCurrentOption();
                        cb.BuildAndRun();
                        Console.WriteLine("");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: {0}", e.Message);
                        if (e is CompilerError)
                        {
                            errorExpr = (e as CompilerError).Expr;
                        }
                        else
                        {
                            errorExpr = Uid.Default;
                        }
                    }
                }
                else if (mod == ConsoleModifiers.Control) {
					if (key == ConsoleKey.Q || key == ConsoleKey.A) {
						cb.OptionIndex += key == ConsoleKey.A ? -1 : 1;
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

					if (subs [i] == errorExpr) {

						Console.BackgroundColor = ConsoleColor.DarkRed;
						if (subs [i] == Uid.Default)
							Console.Write (" ");
						else
							Console.Write (cb.StringOf (subs [i]));
						Console.ResetColor ();

					} else {
						Console.Write (cb.StringOf (subs [i]));
					}
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
