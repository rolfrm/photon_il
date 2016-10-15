using System;
using System.Reflection;

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
			Uid Expr;
			int Index;
		}

		IlGen gen;
		Uid expr;

		public Uid SelectedExpression;
		public int SelectedIndex;
		public int NArguments{ get { return gen.SubExpressions.Get (SelectedExpression).Length; } }
		public string CurrentString;
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
			for (int i = sexp.Length - 1; i > index; i--)
				sexp [i] = sexp [i - 1];
			sexp [index] = Uid.Default;
		}

		public void PushArgument()
		{
			gen.SubExpressions.Add (expr, Uid.Default);
			SelectedIndex = NArguments - 1;
		}

		public void SetString(string str){
			throw new NotImplementedException();
		}

		public string GetString(){
			throw new NotImplementedException();
		}

		public Uid[] GetOptions(){
			throw new NotImplementedException();
		}

		public void SelectOption(Uid option){
			throw new NotImplementedException();
		}

		public void InsertExpr(){
			throw new NotImplementedException();
		}

		public MethodInfo Build(){
			throw new NotImplementedException();
		}

	}
}

