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
		public Uid FirstExpression { get { return gen.SubExpressions.Get (SelectedExpression).FirstOrDefault (); } }
		public int NArguments{ get { return gen.SubExpressions.Get (SelectedExpression).Count; } }
		public string CurrentString{
			get { 
				return Replacements.Get (CurrentItem);
			}
		}
		ASTItem CurrentItem { get { return new ASTItem{ Expr = SelectedExpression, Index = SelectedIndex }; } }
		Dict<ASTItem,string> Replacements = new Dict<ASTItem, string> ();
		Dict<ASTItem, int> OptionIndexes = new Dict<ASTItem, int> ();
		MultiDict<Uid, Uid> Locals = new MultiDict<Uid, Uid> ();

		public IEnumerable<Uid> GetParentExpressions(Uid start){
			
			while (start != Uid.Default) {
				yield return start;
				start = gen.SubExpressions.Entries.FirstOrDefault (x => x.Value.Contains (start)).Key;
			}
		}

		public CodeBuilder (IlGen gen, Uid fid = default(Uid))
		{
			if (fid == Uid.Default) {
				fid = Uid.CreateNew ();
			}
			Function = fid;

			if (gen.GetFunctionBody (Function) != Uid.Default) {
				SelectedExpression = gen.GetFunctionBody (Function);
				SelectedIndex = -1;
			} else{
				if (gen.FunctionInvocation.ContainsKey (fid))
					throw new Exception ("Unable to edit function with no body");
				Uid body = gen.Sub ();

				gen.DefineFcnBody (fid, body);

				SelectedExpression = body;
				SelectedIndex = -1;
			}
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

		public void PushArgument(string strcontent, Uid option = default(Uid)){
			PushArgument ();
			SetString (strcontent);
			if (option == Uid.Default) {
				foreach (var opt in GetOptions ()) {
					if (option == Uid.Default)
						option = opt;
					var strrep = StringOf (opt);
					if (string.IsNullOrWhiteSpace(strrep) == false && strcontent.StartsWith (StringOf (opt))) {
						option = opt;
						break;
					}
				}
			}
			SelectOption (option);
		}


		public void SetString(string str){
			Replacements[CurrentItem] = str;
		}

		public string GetString(){
			return Replacements.Get (CurrentItem) ?? StringOf(CurrentExpression);
		}

		IEnumerable<Uid> getOptions(string str){
			if (ArgumentLists.Contains (SelectedExpression)) {
				foreach (var tp in gen.generatedStructs.Keys)
					yield return tp;
				yield break;
			}

			var spec = gen.GetMacroSpec (FirstExpression);
			if (spec != null) {
                var results = gen.InvokeMacroSpec(gen.GetFunctionBody(Function), SelectedExpression, SelectedIndex, str);
				if (results.Length > 0) {
					foreach(var item in results)
						yield return item;
					yield break;
				}
			}

			if (string.IsNullOrEmpty (str))
				yield break;

			if (SelectedIndex > 0) {
				var args = gen.FunctionArguments.Get (FirstExpression);
				if (args.Count > 0 && SelectedIndex < args.Count + 1) {
					yield return gen.ArgumentType.Get (args [SelectedIndex - 1]);
					yield break;
				}
			}


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
			foreach (var kv in gen.VariableName)
				if (kv.Value.StartsWith (str))
					yield return kv.Key;
			foreach (var kv in gen.MacroNames)
				if (kv.Value.StartsWith (str))
					yield return kv.Key;
			foreach (var item in GetParentExpressions(SelectedExpression).SelectMany(x => Locals.Get(x))) {
				var argname = gen.ArgumentName.Get (item);
				if (argname != null && argname.StartsWith(str))
					yield return item;
				
			}
			yield return gen.StringType;
		}

		public Uid[] GetOptions(){
			return getOptions (CurrentString).ToArray();
		}

		public void SelectOption(Uid option){

			if (option == gen.F.ArgumentList) {
				CreateSub ();
				Replacements.Remove (CurrentItem);
				return;
			}

			var sexp = gen.SubExpressions.Get (SelectedExpression);

			{
				if (ArgumentLists.Contains (SelectedExpression)) {
					Assert.IsTrue (gen.generatedStructs.ContainsKey (option));
					option = gen.Arg (CurrentString, option);
				}
			}

			{
				if (CurrentString == null) {
					option = Uid.Default;
				} else if(gen.generatedStructs.ContainsKey(option)) {
					Type t = gen.generatedStructs.Get (option);
					try{
						var value = Convert.ChangeType (CurrentString, t);
						if (t != default(Type))
							option = gen.DefineConstant (option, value);
					}catch{
						option = Uid.Default;
					}
				}
			}

			sexp [SelectedIndex] = option;	
			Replacements.Remove (CurrentItem);
		}
		HashSet<Uid> ArgumentLists = new HashSet<Uid>();
		public Uid CreateSub(){
			var exprs = gen.SubExpressions.Get (SelectedExpression);
			exprs [SelectedIndex] = Uid.CreateNew ();
			gen.SubExpressions.Add (exprs [SelectedIndex], new Uid[0]);
			if (GetOptions ().FirstOrDefault () == gen.F.ArgumentList)
				ArgumentLists.Add (exprs [SelectedIndex]);
			
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
			bool wasArgList = ArgumentLists.Contains(SelectedExpression);
			if (wasArgList) {
				var args = gen.SubExpressions.Get (SelectedExpression);
				Locals.Get (parent.Key).Clear ();
				Locals.Add (parent.Key, args);
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
			var body = gen.GetFunctionBody (Function);
			gen.GenExpression (body);
			return gen.GenerateIL (Function);
		}

		public void BuildAndRun(){
			
			var body = gen.GetFunctionBody (Function);
			var args = gen.FunctionArguments.Get (Function).ToArray();
			Uid ret = gen.GenExpression (body, args);
			gen.FunctionReturnType [Function] = ret;
			var m = gen.GenerateIL (Function);
			var result = m.Invoke (null, null);
			if(m.ReturnType != typeof(void))
				Console.WriteLine ("{0}", result);
		}

        public CompilerError TryCompile()
        {
            var body = gen.GetFunctionBody(Function);
            var args = gen.FunctionArguments.Get(Function).ToArray();
            try {
                Uid ret = gen.GenExpression(body, args);
            }catch(CompilerError error)
            {
                return error;
            }
            return null;
        }

		public string StringOf(Uid uid){
			if (uid == Uid.Default)
				return "";
			if (uid == gen.F.ArgumentList)
				return "argument list";
			if (gen.SubExpressions.Contains(uid))
				return "( " + string.Join ("   ", gen.SubExpressions.Get(uid).Select (StringOf)) + " )";
			if (gen.ConstantValue.ContainsKey(uid))
				return gen.ConstantValue [uid]?.ToString () ?? "";
			if (gen.ArgumentName.ContainsKey(uid))
				return gen.ArgumentName [uid];
			if (gen.FunctionName.ContainsKey (uid)) {
				return string.Format("{0} {1}", gen.FunctionName [uid], gen.FunctionArguments.Get(uid).Count);
			}if (gen.MacroNames.ContainsKey (uid))
				return gen.MacroNames [uid];
			if (gen.type_name.ContainsKey (uid))
				return gen.type_name.Get (uid);
			var args = new object[]{ uid };
			using (Interact.Push (gen, null)) {
				foreach (var printer in gen.Printers) {
					var str = printer.Value.Invoke (null, args) as string;
					if (str != null)
						return str;
				}
			}
			return "";//throw new Exception ("Cannot tostring type");

		}

		public void CleanSelectedExpression(){
			var sub = gen.SubExpressions.Get (SelectedExpression);
			sub.RemoveAll (x => x == Uid.Default);
			SelectedIndex = Math.Min (SelectedIndex, sub.Count - 1);
		}

		public int OptionIndex {
			get { return OptionIndexes.Get (CurrentItem); }
			set { 
				var optlen = GetOptions ().Length;
				if (optlen == 0 || value >= optlen || value < 0) {
					OptionIndexes.Remove (CurrentItem);
				} else {	
					OptionIndexes [CurrentItem] = value; 
				}
			}
		}

		public void SelectCurrentOption(){
			var options = GetOptions ();
			var idx = OptionIndex;

			if (idx < 0 || idx >= options.Length || options[idx] == Uid.Default)
				return;
			
			SelectOption (options [idx]);
		}

		public void Restart(){

		}

	}
}

