using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PhotonIl;
using System.Collections;

namespace PhotonIl2
{

	public class CsMethod : ILispMethod
	{
		MethodInfo method;
		public CsMethod(MethodInfo method){
			this.method = method;
			var parameters = method.GetParameters ();
			ArgumentTypes = parameters.Select (x => x.ParameterType).ToArray ();
			ArgumentNames = parameters.Select (x => LispContext.Current.Symbolize (x.Name)).ToArray ();
			ReturnType = method.ReturnType;
		}

		public static CsMethod New(MethodInfo method){
			return new CsMethod (method);
		}


		public object Invoke(params object[] args){

			var arr = args;
			var parameters = method.GetParameters ();
			if (parameters.Length > 0 && parameters.Last ().GetCustomAttribute (typeof(ParamArrayAttribute)) != null) {
				var arr2 = new object[parameters.Length];
				for (int i = 0; i < parameters.Length - 1; i++) {
					arr2 [i] = arr [i];
				}
				arr2 [parameters.Length - 1] = arr.Skip (parameters.Length - 1).ToArray ();
				arr = arr2;
			}
			return method.Invoke (null, arr);
		}

		public Type[] ArgumentTypes { get; private set; }
		public Symbol[] ArgumentNames {get; private set;}
		public Type ReturnType{ get; private set;}

		public override string ToString ()
		{
			return string.Format ("C# {0}", method.Name);
		}
	}
	
}
