using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PhotonIl;
using System.Collections;

namespace PhotonIl2
{

	public class LispMethod : ILispMethod
	{
		public readonly Symbol Name;
		public readonly Type ReturnType;
		public readonly Type[] ArgumentTypes;
		public readonly Symbol[] ArgumentNames;
		public readonly object[] DefaultArguments;
		public readonly object[] Body;

		Symbol[] ILispMethod.ArgumentNames { get { return ArgumentNames; } }
		Type[] ILispMethod.ArgumentTypes { get { return ArgumentTypes; } }
		Type ILispMethod.ReturnType { get { return ReturnType;} }

		public LispMethod(Symbol name, Type returnType, Type[] argumentTypes, 
			object[] defaultArguments, object[] body, Symbol[] argumentNames){
			this.Name = name;
			this.ReturnType = returnType;
			this.ArgumentTypes = argumentTypes;
			this.DefaultArguments = defaultArguments;
			this.ArgumentNames = argumentNames;
			this.Body = body;
		}

		public object Invoke(params object[] arguments){
			var interp = LispInterpreter.Current;
			object ret = null;

			var evalArgs = DefaultArguments.Skip (arguments.Length);
			object[] trueargs = new object[DefaultArguments.Length];
			int i = 0;
			for (; i < arguments.Length; i++) {
				trueargs [i] = arguments [i];
			}

			foreach (var x in evalArgs) {
				if (x == null) {
					throw new Exception ("Unsupported argument");
				}
				trueargs [i++] = interp.Eval (x);
			}
			using (var fcnScope = LispContext.Current.WithScope ()) {
				for (int j = 0; j < trueargs.Length; j++) {
					var variable = fcnScope.AddVariable (ArgumentNames [j], ArgumentTypes [j]);
					variable.Value = trueargs [j];
				}

				foreach (var expr in Body) {
					ret = interp.Eval (expr);
				}
				return ret;
			}
		}

		public override string ToString ()
		{
			return string.Format ("Lisp {0}", Name);
		}

	}
	
}
