using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PhotonIl;
using System.Collections;

namespace PhotonIl2
{

	public class LispContext
	{
		public class TempScope : Scope, IDisposable
		{
			LispContext ctx;
			public TempScope(Scope prevScope, LispContext ctx) : base(prevScope)
			{
				this.ctx = ctx;
			}

			void IDisposable.Dispose(){
				Assert.IsTrue(ctx.Scope == this);
				ctx.PopScope ();
			}
		}

		public TempScope WithScope()
		{
			TempScope o = new TempScope(Scope, this);
			Scope = o;
			return o;
		}

		public Scope Scope { get; private set; } = new Scope(null);

		public Scope PushScope(){
			Scope = new Scope (Scope);
			return Scope;
		}

		public void PopScope(){
			Scope = Scope.ParentScope;
		}

		Dictionary<string, Symbol> Symbols = new Dictionary<string, Symbol>();

		public Type typeFromSymbol(Symbol sym){
			return Scope.GetVariable<Type> (sym).Value;
		}

		public Symbol Symbolize(string str){
			Symbol sym;
			if (!Symbols.TryGetValue (str, out sym)) {
				sym = new Symbol (str);
				Symbols [str] = sym;
			}
			return sym;
		}

		public object Symbolize(object obj)
		{
			if (obj is Cons) 
			{
				Cons c = (Cons)obj;
				while (c != null) {
					c.Car = Symbolize (c.Car);
					c = c.Cdr;
				}
			} 
			else if( obj is string)
				return Symbolize ((string)obj);
			
			return obj;
		}

		public readonly static LispContext Current = new LispContext();

	}
	
}
