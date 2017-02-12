using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PhotonIl;
using System.Collections;


namespace PhotonIl2
{
	public class LispInterpreter
	{
		public static LispInterpreter Current{
			get 
			{
				return LispContext.Current.Scope
					.GetVariable<LispInterpreter>(Sym("lisp")).Value;
			}
		}
		static Symbol Sym(string symbolStr){
			return LispContext.Current.Symbolize (symbolStr);
		}
		public HashSet<object> Macros = new HashSet<object>();
		public Dictionary<Symbol, List<ILispMethod>> KnownMethods = new Dictionary<Symbol, List<ILispMethod>> ();

		void addLispMethod(LispMethod m){
			if (KnownMethods.ContainsKey(m.Name) == false)
				KnownMethods [m.Name] = new List<ILispMethod> ();
			KnownMethods [m.Name].Add (m);
		}

		public static object defun(Symbol name, Cons arguments, params object[] expressions)
		{
			var interp = LispInterpreter.Current;
			List<object> defaultArguments = new List<object> ();
			List<Type> argumentTypes = new List<Type> ();
			List<Symbol> argumentNames = new List<Symbol> ();
			Type returnType = null;
			var last = arguments.LastOrDefault ();
			bool skipLast = (last is Cons) == false;
			foreach (var arg in arguments) {
				if (arg == last && skipLast) {
					returnType = LispContext.Current.typeFromSymbol (((Symbol)last));
					break;
				}
				var cons = (Cons)arg;
				var argname = (Symbol)cons.Car;
				argumentNames.Add (argname);
				cons = cons.Cdr;
				var argtype = LispContext.Current.typeFromSymbol ((Symbol)cons.Car);
				argumentTypes.Add (argtype);
				cons = cons.Cdr;
				if (cons != null) {
					Type t = argtype;
					defaultArguments.Add (interp.analyzeLisp (cons.Car, ref t));
					Assert.IsTrue (t == argtype);
				} else {
					defaultArguments.Add (null);
				}
			}
			using (var scope = LispContext.Current.WithScope ()) {
				for (int i = 0; i < argumentNames.Count; i++)
					scope.AddVariable (argumentNames [i], argumentTypes [i]);
				for (int i = 0; i < expressions.Length; i++) {
					
					Type t = null;
					expressions[i] = interp.analyzeLisp(expressions[i], ref t);
					if (i == expressions.Length - 1)
						Assert.IsTrue(t == returnType);
				}

				var lispMethod = new LispMethod (name, returnType, 
					argumentTypes.ToArray (), 
					defaultArguments.ToArray (), expressions, argumentNames.ToArray());
				
				interp.addLispMethod (lispMethod);
				return name.String;
			}
		}

		List<ILispMethod> getMethods(Symbol sym){
			
			if (KnownMethods.ContainsKey (sym) == false) {
				var symbol = sym.String;
				var lastdot = symbol.LastIndexOf ('.');
				var fcnname = symbol.Substring (lastdot + 1);
				var type = symbol.Substring (0, lastdot);
				var tp = Type.GetType (type);
				var m = tp.GetMethods (fcnname);
				KnownMethods [sym] = m.Select (CsMethod.New).ToList<ILispMethod>();
			}
			if (KnownMethods.ContainsKey (sym)) {
				return KnownMethods [sym];
			}
			return new List<ILispMethod> (0);
		}

		public object analyzeLisp(object obj, ref Type type)
		{
			if (obj is Cons) {
				var cons = obj as Cons;
				var func = cons.Car;

				if (func is Symbol) {
					var symbol = func as Symbol;
					var methods2 = getMethods (symbol);
					foreach (ILispMethod method in methods2) {
						if (Macros.Contains (method)) {
							var arr = cons.Cdr.ToArray ();
							var r = method.Invoke (arr);
							return analyzeLisp (r, ref type);
						}
					} 

					List<Type> argTypes = new List<Type> ();
					foreach (Cons c in cons.Cdr.EnumerateCons()) {
						Type t = null;
						c.Car = analyzeLisp (c.Car, ref t);
						argTypes.Add (t);
					}

					List<ILispMethod> lst = new List<ILispMethod> ();
					foreach (ILispMethod method in methods2) {
						if (method.ArgumentTypes.SequenceEqual (argTypes))
							lst.Add (method);
					}

					if (lst.Count > 0) {
						cons.Car = lst;
						var fst = lst [0];
						var mi = fst as ILispMethod;
						if (mi != null)
							type = mi.ReturnType;
					} else {
						throw new Exception ("No method matches function types");
					}
				} else if (func is ILispMethod) {
					var f = (ILispMethod)func;
					List<Type> argTypes = new List<Type> ();
					foreach (Cons c in cons.Cdr) {
						Type t = null;
						c.Car = analyzeLisp (c.Car, ref t);
						argTypes.Add (t);
					}
					Assert.IsTrue (argTypes.SequenceEqual (f.ArgumentTypes));
					type = f.ReturnType;
				}
			} else if (obj is Symbol) {
				var sym = obj as Symbol;
				if (sym != null) {
					var v = LispContext.Current.Scope.GetVariable (sym);
					if (v != null)
						type = v.Type;
					else
						type = typeof(Symbol);
				}
			}
			return obj;
		}

		public object Eval(object obj)
		{
			obj = LispContext.Current.Symbolize (obj);
			Cons cons = obj as Cons;
			if (cons != null) {
				var methods = cons.Car as List<ILispMethod>;
				if (methods != null) {
					var args = cons.Cdr.Select (Eval).ToArray ();
					var m = methods[0] as ILispMethod;
					if (m != null)
						return m.Invoke (args);
				}
				throw new Exception ("Invalid expression");
			} 
			Symbol sym = obj as Symbol;
			if (sym != null) {
				var variable = LispContext.Current.Scope.GetVariable (sym);
				if (variable != null)
					return variable.Value;
			}
			return obj;
		}

	}
}

