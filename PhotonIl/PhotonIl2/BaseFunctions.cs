using System;
using PhotonIl;

namespace PhotonIl2
{
	public static class BaseFunctions
	{
		static Symbol Sym(string symbolStr){
			return LispContext.Current.Symbolize (symbolStr);
		}

		public static int i32 (Symbol input)
		{
			int o;
			if (!Int32.TryParse (input.String, out o))
				throw new Exception ("Unable to parse");
			return o;
		}

		public static float f32 (Symbol input)
		{
			float o;
			if (!float.TryParse (input.String, out o))
				throw new Exception ("Unable to parse");
			return o;
		}

		public static string str (Symbol input)
		{
			return input.String;
		}

		public static int add (int x, int y)
		{
			return x + y;
		}

		public static float add (float x, float y)
		{
			return x + y;
		}

		public static float add (float x, float y, float z)
		{
			return x + y + z;
		}


		public static void defvar(Symbol name, Type type)
		{
			LispContext.Current.Scope.AddVariable(name, type);
		}

		public static void _setvar(IVariable val, object value){
			val.Value = value;
		}

		public static object setvar(Symbol name, Cons value){
			var variable = LispContext.Current.Scope.GetVariable(name);
			Assert.IsTrue (variable != null);
			Type t = null;
			object r = LispInterpreter.Current.analyzeLisp (value, ref t);
			Assert.IsTrue (t == variable.Type);
			return Cons.FromValues (typeof(BaseFunctions).GetLispMethods("_setvar"), variable, r);
		}


		public static void Load(LispInterpreter interp){
			
			interp.KnownMethods [Sym ("i32")] = typeof(BaseFunctions).GetLispMethods ("i32");
			interp.KnownMethods [Sym ("f32")] = typeof(BaseFunctions).GetLispMethods ("f32");
			interp.KnownMethods [Sym ("str")] = typeof(BaseFunctions).GetLispMethods ("str");
			interp.KnownMethods [Sym ("+")] = typeof(BaseFunctions).GetLispMethods ("add");
			interp.KnownMethods [Sym ("defun")] = typeof(LispInterpreter).GetLispMethods ("defun");
			interp.KnownMethods [Sym ("defvar")] = typeof(BaseFunctions).GetLispMethods ("defvar");
			interp.KnownMethods [Sym ("setvar")] = typeof(BaseFunctions).GetLispMethods ("setvar");

			var defuns = interp.KnownMethods [Sym ("defun")];
			foreach (var x in defuns)
				interp.Macros.Add (x);

			var _sets = interp.KnownMethods [Sym ("setvar")];
			foreach (var x in _sets)
				interp.Macros.Add (x);

			LispContext.Current.Scope.AddVariable<Type> (Sym ("i32")).Value = typeof(int);
			LispContext.Current.Scope.AddVariable<Type> (Sym ("f32")).Value = typeof(float);
		}
	}
}

