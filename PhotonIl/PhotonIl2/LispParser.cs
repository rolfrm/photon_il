using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace PhotonIl2
{
	public class Symbol
	{
		public readonly string String;
		public Symbol(string str){
			String = str;
		}

		public override string ToString ()
		{
			return string.Format ("'{0}", String);
		}
	}

	public class Cons : IEnumerable<object>
	{
		public Cons Cdr;
		public Object Car;
		public IEnumerable<object> GetValues(){
			Cons cons = this;
			while ( cons != null) {
				yield return cons.Car;
				cons = cons.Cdr;
			}
		}

		public IEnumerable<Cons> EnumerateCons(){
			Cons c = this;
			while (c != null) {
				yield return c;
				c = c.Cdr;
			}
		}

		public IEnumerator<object> GetEnumerator(){
			return GetValues ().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator(){
			return this.GetEnumerator();
		}

		public static Cons FromValues(params object[] values){
			Cons c = null;
			for (int i = values.Length - 1; i >= 0; i--) {
				c = new Cons {
					Car = values[i],
					Cdr = c
				};
			}
			return c;
		}
	}

	public class LispParser
	{
		public static string FormatLispObject(object lispObj)
		{
			var cons = lispObj as Cons;
			if (cons != null)
				return string.Format ("({0})", 
					string.Join (" ", cons.Select (FormatLispObject)));
			else {
				var str = lispObj.ToString ();
				if (str.Contains (' '))
					return '"' + str + '"';
				return str;
			}

		}

		TextReader str;
		public LispParser (TextReader str)
		{
			this.str = str;	
		}

		public Object Next()
		{
			skipWhiteSpace ();
			if (nextIsChar ('(')) {

				Cons cons = new Cons ();
				Cons _cons = cons;
				str.Read ();
				bool first = true;
				while (!(nextIsChar (')', true))) {
					if (first)
						first = false;
					else {
						Cons expr = new Cons ();	
						cons.Cdr = expr;
						cons = cons.Cdr;
					}
					cons.Car = Next ();
				}
				str.Read ();
				return _cons;
			} else {
				int nxt = str.Peek ();
				if (nxt == -1)
					return null;
				if (nxt == ';') {
					str.ReadLine ();
					return Next ();
				}
				if (nxt == (int)'"') {
					str.Read ();
					var literalString = takeWhile (x => x != '"');
					str.Read ();
					return literalString;
				}

				var k = takeWhile (x =>!(x == ')' || char.IsWhiteSpace(x) || x == ';'));
				return k;
			}
		}

		bool nextIsChar(char chr, bool skipWhiteSpace = false){
			if (skipWhiteSpace)
				this.skipWhiteSpace ();
			return str.Peek () == (int)chr;
		}

		void skipWhiteSpace(){
			skipWhile (Char.IsWhiteSpace);
		}

		void skipWhile(Func<char,bool> f){
			while (true) {
				int pk = str.Peek ();
				if (pk == -1)
					return;
				char chr = (char)pk;
				if (!f (chr))
				    break;
			    str.Read ();
			}
		}
		string takeWhile(Func<char, bool> f){
			StringBuilder sb = new StringBuilder ();
			while (true) {
				int pk = str.Peek ();
				if (pk == -1)
					break;
				char chr = (char)pk;
				if (!f (chr))
					break;
				sb.Append (chr);
				str.Read ();
			}
			return sb.ToString ();
		}
	}
}

