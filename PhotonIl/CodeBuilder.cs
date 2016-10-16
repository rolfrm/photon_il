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
		public Uid Function;
		public Uid SelectedExpression;
		public int SelectedIndex;
		public int NArguments{ get { return gen.SubExpressions.Get (SelectedExpression).Count; } }
		public string CurrentString{
			get { 
				return Replacements.Get (new ASTItem{ Expr = SelectedExpression, Index = SelectedIndex });
			}
		}
		Dict<ASTItem,string> Replacements = new Dict<ASTItem, string> ();
		public CodeBuilder (IlGen gen, Uid fid = default(Uid))
		{
			if (fid == Uid.Default) {
				fid = Uid.CreateNew ();
			}
			Function = fid;

			Uid body = gen.Sub ();

			gen.DefineFcnBody (fid, body);

			SelectedExpression = body;
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
			Replacements[item] = str;
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

		public Uid CreateSub(){
			var exprs = gen.SubExpressions.Get (SelectedExpression);
			exprs [SelectedIndex] = Uid.CreateNew ();
			return exprs [SelectedIndex];
		}

		public void Enter(){
			var exprs = gen.SubExpressions.Get (SelectedExpression);
			SelectedExpression = exprs [SelectedIndex];

			SelectedIndex = -1;
		}

		public void Exit(){
			var parent = gen.SubExpressions.Entries.FirstOrDefault (x => x.Value.Contains (SelectedExpression));
			var idx = parent.Value.IndexOf (SelectedExpression);
			SelectedExpression = parent.Key;
			SelectedIndex = idx;
		}

		public MethodInfo Build(){
			
			gen.GenExpression (Function);
			return gen.GenerateIL (Function);
		}

		public void BuildAndRun(){
			
			var body = gen.GetFunctionBody (Function);
			Uid ret = gen.GenExpression (body);

			Uid fcn = gen.DefineFunction ("run", gen.VoidType);
			if (ret != gen.VoidType) {
				var body2 = gen.Sub (gen.F.PrintAny, body);
				gen.DefineFcnBody (fcn, body2);
			}
			var m = gen.GenerateIL (fcn);
			m.Invoke (null, null);

		}
	}
}

