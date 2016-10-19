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
		int selectedIndex = -1;
		public int SelectedIndex{
			get{ return selectedIndex; }
			set{
				if (value < 0)
					selectedIndex = -1;
				else if (value >= NArguments)
					selectedIndex = NArguments - 1;
				else  
					selectedIndex = value;
			}
		}
			
		public Uid CurrentExpression{ get { return SelectedIndex == -1 ? Uid.Default : gen.SubExpressions.Get (SelectedExpression) [SelectedIndex]; } }
		public int NArguments{ get { return gen.SubExpressions.Get (SelectedExpression).Count; } }
		public string CurrentString{
			get { 
				return Replacements.Get (CurrentItem);
			}
		}
		ASTItem CurrentItem { get { return new ASTItem{ Expr = SelectedExpression, Index = SelectedIndex }; } }
		Dict<ASTItem,string> Replacements = new Dict<ASTItem, string> ();
		Dict<ASTItem, int> OptionIndexes = new Dict<ASTItem, int> ();

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
			Replacements[CurrentItem] = str;
		}

		public string GetString(){
			return Replacements.Get (CurrentItem) ?? StringOf(CurrentExpression);
		}

		IEnumerable<Uid> getOptions(string str){
			if (string.IsNullOrEmpty (str))
				yield break;
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
			yield return gen.StringType;
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
			Replacements.Remove (CurrentItem);
		}

		public Uid CreateSub(){
			var exprs = gen.SubExpressions.Get (SelectedExpression);
			exprs [SelectedIndex] = Uid.CreateNew ();
			gen.SubExpressions.Add (exprs [SelectedIndex], Uid.Default);
			return exprs [SelectedIndex];
		}

		public void Enter(){
			if (gen.SubExpressions.Contains (CurrentExpression) == false )
				return;
			SelectedExpression = CurrentExpression;
			SelectedIndex = -1;
		}

		public void Exit(){
			var parent = gen.SubExpressions.Entries.FirstOrDefault (x => x.Value.Contains (SelectedExpression));
			if (parent.Value == null) {
				SelectedExpression = Uid.Default;
				SelectedIndex = -1;
				return;
			}
			var idx = parent.Value.IndexOf (SelectedExpression);
			SelectedExpression = parent.Key;
			SelectedIndex = idx;
		}

		public void Delete(){
			if (selectedIndex < 0)
				return;
			var exprs = gen.SubExpressions.Get (SelectedExpression);
			exprs [SelectedIndex] = Uid.Default;
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

		public string StringOf(Uid uid){
			if (uid == Uid.Default)
				return "";
			var sub = gen.SubExpressions.Get(uid);
			if (sub.Count > 0)
				return "( " + string.Join ("   ", sub.Select (StringOf)) + " )";
			if (gen.ConstantValue.ContainsKey(uid))
				return gen.ConstantValue [uid].ToString ();
			if (gen.ArgumentName.ContainsKey(uid))
				return gen.ArgumentName [uid];
			if (gen.FunctionName.ContainsKey(uid))
				return gen.FunctionName [uid];
			if (gen.MacroNames.ContainsKey (uid))
				return gen.MacroNames [uid];
			if (gen.type_name.ContainsKey (uid))
				return gen.type_name.Get (uid);
			return "";//throw new Exception ("Cannot tostring type");

		}

		public void CleanSelectedExpression(){
			var sub = gen.SubExpressions.Get (SelectedExpression);
			sub.RemoveAll (x => x == Uid.Default);
			SelectedIndex = Math.Min (SelectedIndex, sub.Count - 1);
		}

		public int OptionIndex {
			get {
				return OptionIndexes.Get (CurrentItem);
			}
			set {
				OptionIndexes[CurrentItem] = value;
			}
		}

		public void SelectCurrentOption(){
			var options = GetOptions ();
			var idx = OptionIndex;
			if (idx < 0 || idx >= options.Length)
				return;
			SelectOption (options [idx]);
		}

	}
}

