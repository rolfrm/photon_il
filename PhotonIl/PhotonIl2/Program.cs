using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;


namespace PhotonIl2
{
	class MainClass
	{		
		public static void Main (string[] args)
		{
			var parser = new LispParser (System.IO.File.OpenText("example.lisp"));
			var interp = new LispInterpreter ();
			BaseFunctions.Load (interp);
			var interpv = LispContext.Current.Scope.AddVariable<LispInterpreter> (LispContext.Current.Symbolize ("lisp"));
			interpv.Value = interp;
			object obj = null;
			while ((obj = parser.Next ()) != null) {
				var str = LispParser.FormatLispObject (obj);
				Console.WriteLine ("Read: " + str);
				Type t = null;
				var symobj = LispContext.Current.Symbolize (obj);
				var symstr = LispParser.FormatLispObject (symobj);
				Console.WriteLine ("Sym: " + symstr);
				var code = interp.analyzeLisp (symobj, ref t);
				var codestr = LispParser.FormatLispObject (code);
				Console.WriteLine ("Sym: " + codestr);
				var result = interp.Eval (code);
				Console.WriteLine ("eval: {0}", result);
			}
		}
	}
}
