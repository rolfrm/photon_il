using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace PhotonIl
{
	// A kind of state machine to manage the creation of code.
	// It is meant to be the only interface through which the GUI genereates code,
	// which is an iterative process.
	// 
	public class CodeBuilder
	{
		struct ASTItem
		{
			public Uid Expr;
			public int Index;
		}

		IlGen gen;

		public Uid SelectedExpression;
		public int SelectedIndex;
		public int NArguments{ get { return gen.SubExpressions.Get (SelectedExpression).Count; } }
		public string CurrentString{
			get { 
				return Replacements.Get (new ASTItem{ Expr = SelectedExpression, Index = SelectedIndex });
			}
		}
		Dict<ASTItem,string> Replacements = new Dict<ASTItem, string> ();
		public CodeBuilder (IlGen gen, Uid expr = default(Uid))
		{
			SelectedExpression = expr;
			SelectedIndex = -1;
			this.gen = gen;
		}

		public void AddArgument(int index)
		{
			PushArgument ();
			var sexp = gen.SubExpressions.Get (SelectedExpression);
			for (int i = sexp.Count - 1; i > index; i--)
				sexp [i] = sexp [i - 1];
			sexp [index] = Uid.Default;
			SelectedIndex = index;
		}

		public void PushArgument()
		{
			gen.SubExpressions.Add (SelectedExpression, Uid.Default);
			SelectedIndex = NArguments - 1;
		}

		public void SetString(string str){
			var item = new ASTItem{ Expr = SelectedExpression, Index = SelectedIndex };
			Replacements.Add (item,str);
		}

		public string GetString(){
			var item = new ASTItem{ Expr = SelectedExpression, Index = SelectedIndex };
			return Replacements.Get (item) ?? "";
		}

		IEnumerable<Uid> getOptions(string str){
			float rf;
			double rd;
			uint ru;
			int ri;
			byte rb;
			ushort rus;
			if (float.TryParse (str, out rf))
				yield return gen.F32Type;
			if (double.TryParse (str, out rd))
				yield return gen.F64Type;
			if (int.TryParse (str, out ri))
				yield return gen.I32Type;
			if (uint.TryParse (str, out ru))
				yield return gen.U32Type;
			if (ushort.TryParse (str, out rus))
				yield return gen.U16Type;
			if (byte.TryParse (str, out rb))
				yield return gen.U8Type;
			foreach (var kv in gen.FunctionName)
				if (kv.Value.StartsWith (str))
					yield return kv.Key;
			foreach (var kv in gen.variableName)
				if (kv.Value.StartsWith (str))
					yield return kv.Key;
			foreach (var kv in gen.MacroNames)
				if (kv.Value.StartsWith (str))
					yield return kv.Key;
		}

		public Uid[] GetOptions(){
			return getOptions (CurrentString).ToArray();
		}

		public void SelectOption(Uid option){
			var sexp = gen.SubExpressions.Get (SelectedExpression);
			{
				Type t = gen.generatedStructs.Get (option);
				if (t != default(Type)) {
					object obj = Convert.ChangeType (CurrentString, t);
					option = gen.DefineConstant (option, obj);
				}
			}

			sexp [SelectedIndex] = option;	
		}
	}
}

