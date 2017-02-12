using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PhotonIl;
using System.Collections;

namespace PhotonIl2
{

	public class Scope
	{
		public readonly Scope ParentScope;
		public Scope(Scope parentScope){
			this.ParentScope = parentScope;
		}

		Dictionary<Symbol, IVariable> Variables = new Dictionary<Symbol, IVariable> ();

		public IVariable GetVariable(Symbol sym){
			IVariable ret = null;
			if (!Variables.TryGetValue (sym, out ret)) {
				if (ParentScope != null)
					return ParentScope.GetVariable (sym);
				else
					return null;
			}
			return ret;
		}

		public Variable<T> GetVariable<T>(Symbol sym){
			return (Variable<T>)GetVariable (sym);
		}
		public IVariable AddVariable (Symbol sym, Type variableType)
		{
			var variable = (IVariable)Activator.CreateInstance (typeof(Variable<>).MakeGenericType (variableType));
			Variables.Add (sym, variable);
			return variable;
		}

		public Variable<T> AddVariable<T>(Symbol sym){
			var newvar = new Variable<T> ();
			Variables.Add (sym, newvar);
			return newvar;
		}

	}
	
}
