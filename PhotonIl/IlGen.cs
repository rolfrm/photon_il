using System.Reflection.Emit;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using ProtoBuf.Meta;
using ProtoBuf;

namespace PhotonIl
{
	public class CompilerError : Exception
	{
		public readonly Uid Expr;

		public CompilerError (Uid expr, string msg, params object[] args) : base (string.Format (msg, args))
		{
			Expr = expr;
		}
	}

	public enum Types
	{
		Primitive,
		Pointer,
		Function,
		Struct
	}

	public class IlGen : IPhotonModule
	{
		public BaseFunctions F { get { return GetModule<BaseFunctions> (); } }
		public ArrayModule A { get { return GetModule<ArrayModule> (); } }

		public Dict<Uid, Types> types = new Dict<Uid, Types> ();
		public Dict<Uid, string> type_name = new Dict<Uid, string> ();
		public Dict<Uid, int> type_size = new Dict<Uid, int> ();
		public Dict<Uid, bool> is_floating_point = new Dict<Uid, bool> ();

		public Dict<Uid, Uid> FunctionReturnType = new Dict<Uid, Uid> ();
		public Dict<Uid, MethodInfo> FunctionInvocation = new Dict<Uid, MethodInfo> ();
		public Dict<Uid, string> FunctionName = new Dict<Uid, string> ();
		MultiDict<Uid, Uid> FunctionArguments = new MultiDict<Uid, Uid> ();
		Dict<Uid, Uid> functionBody = new Dict<Uid, Uid> ();

		public Dict<Uid, Func<Uid, IlGen, Uid>> Macros = new Dict<Uid, Func<Uid, IlGen, Uid>> ();
		public readonly Uid U8Type;
		public readonly Uid U16Type;
		public readonly Uid U32Type;
		public readonly Uid I32Type;
		public readonly Uid F32Type;
		public readonly Uid F64Type;
		public readonly Uid VoidType;
		public readonly Uid StringType;
		public readonly Uid UidType;

		public HashSet<Uid> Expressions = new HashSet<Uid> ();
		public MultiDict<Uid, Uid> SubExpressions = new MultiDict<Uid, Uid> ();
		public Dict<Uid, string> VariableName = new Dict<Uid, string> ();
		public Dict<Uid, object> VariableValue = new Dict<Uid, object> ();
		public Dict<Uid, Uid> VariableType = new Dict<Uid, Uid> ();

		public readonly Dict<Uid, object> ConstantValue = new Dict<Uid, object> ();
		public readonly Dict<Uid, Uid> ConstantType = new Dict<Uid, Uid> ();
		Dict<Uid,FieldInfo> variableItems = new Dict<Uid, FieldInfo> ();
		public delegate Type TypeGetter (Uid expr);

		public readonly List<TypeGetter> TypeGetters = new List<TypeGetter> ();

		AssemblyBuilder builder;
		ModuleBuilder modBuilder;
		Guid asmId = Guid.NewGuid();
		string AssemblyName () => $"{asmId}.dll";

		HashSet<Guid> loadedAssemblies = new HashSet<Guid>();

		public readonly Uid Add;
		public readonly Uid Subtract;
		public readonly Uid Multiply;
		public readonly Uid Divide;
		public readonly Uid RightShift;
		public readonly Uid LeftShift;
		public readonly Uid BitOr;
		public readonly Uid BitAnd;
		public readonly Uid Modulus;

		Dict<string, Uid> SymbolNames = new Dict<string, Uid> ();
		HashSet<Uid> Symbols = new HashSet<Uid> ();
		Dict<Uid,MacroDelegate> userMacros = new Dict<Uid, MacroDelegate> ();

		public delegate Uid MacroDelegate (Uid expr);
		public delegate Uid MacroSpecDelegate (Uid expr, int index, string suggestion);

		Dict<Uid,MacroSpecDelegate> macroSpecs = new Dict<Uid, MacroSpecDelegate> ();

		public void Load(IlGen gen){

		}

		public void LoadData(Assembly asm, object serialized){
			var lut = (Dict<Uid, Tuple<string, string>>)serialized;
			foreach (var item in lut) {
				var type = asm.GetType (item.Value.Item1);
				if (type == null)
					continue;
				var method = type.GetMethod (item.Value.Item2);
				FunctionInvocation [Uid.CreateNew ()] = method;
			}
		}
		public object Save(){
			var functionTypeNames = new Dict<Uid, Tuple<string, string>>();
			foreach (var f in FunctionInvocation) {
				functionTypeNames [f.Key] = Tuple.Create(f.Value.DeclaringType.FullName, f.Value.Name);
			}
			return functionTypeNames;
		}
		Dict<Type, IPhotonModule> modules = new Dict<Type, IPhotonModule>();

		public IPhotonModule GetModule(Type t){
			if (false == modules.ContainsKey (t)) 
			{
				modules [t] = (IPhotonModule)Activator.CreateInstance (t);
				modules [t].Load (this);
			}
			return modules [t];

		}

		public T GetModule<T>() where T: IPhotonModule
		{
			return (T)GetModule (typeof(T));
		}


		public Uid AddPrimitive (string name, int size, Type dotNetType = null, bool is_float = false)
		{
			var prim = Uid.CreateNew ();
			types.Add (prim, Types.Primitive);
			type_size.Add (prim, size);
			type_name.Add (prim, name);
			if (is_float)
				is_floating_point.Add (prim, is_float);
			generatedStructs.Add (prim, dotNetType);
			return prim;
		}

		Uid addCSharpType (Type type)
		{
			if (type.IsValueType == false)
				throw new Exception ("Unable to add non-value type");

			Uid uid = Uid.CreateNew ();

			this.types.Add (uid, Types.Struct);
			this.generatedStructs.Add (uid, type);
			this.type_name.Add (uid, type.Name);
			return uid;
		}

		public IlGen (bool bare = false)
		{
			modules [typeof(IlGen)] = this;
			if (bare)
				return;
			U8Type = AddPrimitive ("u8", 1, typeof(byte));
			U16Type = AddPrimitive ("u16", 2, typeof(short));
			U32Type = AddPrimitive ("u32", 4, typeof(uint));
			I32Type = AddPrimitive ("i32", 4, typeof(int));
			F32Type = AddPrimitive ("f32", 4, typeof(float), is_float: true);
			F64Type = AddPrimitive ("f64", 8, typeof(double), is_float: true);

			VoidType = AddPrimitive ("void", 0);
			StringType = AddPrimitive ("string", 0, typeof(string));
			UidType = addCSharpType (typeof(Uid));
			Assert.IsTrue (this.type_size.Get (U8Type) == 1);
			F.ToString ();A.ToString ();


			Add = genBaseFunctor (OpCodes.Add, "+");
			Subtract = genBaseFunctor (OpCodes.Sub, "-");
			Multiply = genBaseFunctor (OpCodes.Mul, "*");
			Divide = genBaseFunctor (OpCodes.Div, "/");
			RightShift = genBaseFunctor (OpCodes.Shr, ">>");
			LeftShift = genBaseFunctor (OpCodes.Shl, "<<");
			BitOr = genBaseFunctor (OpCodes.Or, "|");
			BitAnd = genBaseFunctor (OpCodes.And, "&");
			Modulus = genBaseFunctor (OpCodes.Rem, "%");

			Macros.Add (Let = Uid.CreateNew (), genLet);
			Macros.Add (Progn = Uid.CreateNew(), genProgn);
			Macros.Add (Set = Uid.CreateNew (), genSet);
			Macros.Add (getStructAccess =  Uid.CreateNew (), genStructAccess);
		}

		/*
		public void AddMacro(Uid symbol, object holder, string methodname){
			var tp = holder.GetType ();
			var method = tp.GetMethod (methodname, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (method != null)
				Macros.Add (symbol, method);
		}*/

		public Uid getPhotonType (Type t)
		{
			var entry = generatedStructs.FirstOrDefault (x => x.Value == t);
			if (entry.Key != Uid.Default)
				return entry.Key;
			throw new Exception ("Type not found: " + t.Name);
		}

		public Uid DefineVariable (Uid type, string name = null, object value = null)
		{
			Uid varid = Uid.CreateNew ();
			if (name != null)
				VariableName.Add (varid, name);
			if (value != null)
				VariableValue.Add (varid, value);
			VariableType.Add (varid, type);

			return varid;
		}

		public Uid DefineConstant (Uid type, object value, string name = null)
		{
			var s = Sym (name);
			ConstantType.Add (s, type);
			ConstantValue.Add (s, value);
			return s;
		}

		FieldInfo getVariable (Uid variableId)
		{
			FieldInfo v = variableItems.Get (variableId);
			if (v == null) {
				var typeid = VariableType.Get (variableId);
				if (typeid == Uid.Default)
					return null;
				
				var name = VariableName.Get (variableId) ?? "AnonVariable";
				var val = VariableValue.Get (variableId);
				var mod = getDynamicModule ();
				var tp = mod.DefineType ("no-name");

				tp.DefineField (name, GetCSType (typeid), FieldAttributes.Public | FieldAttributes.Static); 
				var objtype = tp.CreateType ();
				v = objtype.GetField (name, BindingFlags.Public | BindingFlags.Static);

				if (val != null)
					v.SetValue (null, val);
				variableItems.Add (variableId, v);
			}
			return v;
		}

		public Uid CompileSubExpression (Uid expr)
		{
			{
				var constantType = ConstantType.Get (expr);
				if (constantType != Uid.Default) {
					if (this.types.Get (constantType) == Types.Primitive) {
						int size = this.type_size.Get (constantType);
						bool isfloat = this.is_floating_point.Get (constantType);
						if (isfloat && size == 4)
							Interact.Emit (OpCodes.Ldc_R4, (float)Convert.ChangeType (ConstantValue.Get (expr), typeof(float)));
						else if (isfloat && size == 8)
							Interact.Emit (OpCodes.Ldc_R8, (double)Convert.ChangeType (ConstantValue.Get (expr), typeof(double)));
						else if (size <= 4)
							Interact.Emit (OpCodes.Ldc_I4, (int)Convert.ChangeType (ConstantValue.Get (expr), typeof(int)));
						else if (size == 8)
							Interact.Emit (OpCodes.Ldc_I8, (long)Convert.ChangeType (ConstantValue.Get (expr), typeof(long)));
						else
							throw new Exception ("Cannot load type");
						return constantType;
					}
				}
			}

			{
				var variableMember = getVariable (expr);
				if (variableMember != null) {
					Interact.Emit (OpCodes.Ldnull);
					Interact.Emit (OpCodes.Ldfld, variableMember);
					return VariableType.Get (expr);
				}
			}
            
			if (localSymbols.Value.ContainsKey (expr)) {
				var local = localSymbols.Value.Get (expr);
				if (ReferenceReq.Contains (expr)) {
					if (local.Local != default(LocalBuilder))
						Interact.Emit (OpCodes.Ldloca_S, local.Local);
					else
						Interact.Emit (OpCodes.Ldarga, local.ArgIndex);
					ReferenceReq.Remove (expr);
				} else {
					if (local.Local != default(LocalBuilder))
						Interact.Emit (OpCodes.Ldloc, local.Local);
					else
						Interact.Emit (OpCodes.Ldarg, local.ArgIndex);
				}
				return local.TypeId;
			}
			var subexprs = SubExpressions.Get (expr);
			if (subexprs.Count == 0)
				throw new CompilerError (expr, "Invalid expression");
			{
				var gen = Macros.Get (subexprs [0]);
				if (gen != null)
					return gen (expr, this);
			}

			{	
				var gen = this.userMacros.Get (subexprs [0]);
				if (gen != null) {
					var ret = gen (expr);
					if (SubExpressions.Get (ret).Count == 0)
						return ret;
					else
						return CompileSubExpression (ret);
				}
			}


			return CompileFunctionCall (expr);

		}

		public Type GetCSType (Uid typeid)
		{
			if (typeid == VoidType)
				return typeof(void);
			if (generatedStructs.Get (typeid) != null) {
				return generatedStructs.Get (typeid);
			} else if (types.Get (typeid) == Types.Struct) {
				return getStructType (typeid);
			}
			foreach (var f in TypeGetters) {
				Type t = f (typeid);
				if (t != null)
					return t;
			}

			throw new Exception ("Unsupported type");
		}



		Type getStructType (Uid typeid)
		{
			if (generatedStructs.ContainsKey (typeid) == false) {
				var structNameVar = structName.Get (typeid);
				var name = VariableValue.Get (structNameVar) as string;
				var typebuilder = getDynamicModule ().DefineType (name ?? "uniqueid",
					                              TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
					                              typeof(ValueType));
				var members = structMembers.Get (typeid);
				foreach (var member in members)
					typebuilder.DefineField (ArgumentName.Get (member), GetCSType (ArgumentType.Get (member)),
						FieldAttributes.Public);
				generatedStructs.Add (typeid, typebuilder.CreateType ());
			}
			return generatedStructs.Get (typeid);
		}

		public Dict<Uid, Type> generatedStructs = new Dict<Uid, Type> ();

		public Uid CompileFunctionCall (Uid expr)
		{
			var subexprs = SubExpressions.Get (expr);
			var function = subexprs [0];
			if (FunctionInvocation.Get (function) == null) {
				GenerateIL (function);
			}
			var mt = function;
			var args = FunctionArguments.Get (function);
			var returnType = FunctionReturnType.Get (mt);

			if (args.Count != subexprs.Count - 1)
				throw new CompilerError (expr, $"Unsupported number of arguments. Supported: {args.Count}, got: {subexprs.Count -1}");

			LocalBuilder[] stlocs = new LocalBuilder[subexprs.Count - 1];
			for (int i = 1; i < subexprs.Count; i++) {

				Uid type = CompileSubExpression (subexprs [i]);
				if (type != ArgumentType.Get (args [i - 1]))
					throw new CompilerError (subexprs [i], "Invalid type of arg {0}. Expected {1}, got {2}.", i - 1, args [i - 1], type);
				stlocs [i - 1] = Interact.DeclareLocal (GetCSType (type));
				Interact.Emit (OpCodes.Stloc, stlocs [i - 1]);
			}

			var m = FunctionInvocation.Get (function);

			foreach (var loc in stlocs)
				Interact.Emit (OpCodes.Ldloc, loc);
			Interact.Emit (OpCodes.Call, m);

			return returnType;
		}

		ModuleBuilder getDynamicModule ()
		{
			if (builder == null) {
				
				builder = AppDomain.CurrentDomain.DefineDynamicAssembly (
					new AssemblyName (AssemblyName()),
					AssemblyBuilderAccess.RunAndSave, System.IO.Directory.GetCurrentDirectory ());
				loadedAssemblies.Add (asmId);
				modBuilder = builder.DefineDynamicModule (AssemblyName(),AssemblyName(),true);
				CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder (typeof(System.Runtime.InteropServices.GuidAttribute).GetConstructors()[0], new object[]{asmId.ToString ()});
				builder.SetCustomAttribute (attrBuilder);
			}
			return modBuilder;
		}

		public Uid GenExpression (Uid expr, params Uid[] arguments)
		{
			Interact.Load (this, null);
			Uid rt;
			using (localSymbols.WithValue (new Dict<Uid, LocalSymData> ())) {
					
				short paramIndex = 0;
				foreach (var arg in arguments) {
					localSymbols.Value.Add (arg, new LocalSymData{ ArgIndex = paramIndex, TypeId = ArgumentType.Get (arg) });
					paramIndex += 1;
				}
				rt = CompileSubExpression (expr);
			}
			return rt;
		}
		int typeid = 0;
		public MethodInfo GenerateIL (Uid expr)
		{
			var module = getDynamicModule ();
			var tb = module.DefineType ($"MyType{typeid++}", TypeAttributes.Class | TypeAttributes.Public);

			var name = FunctionName.Get (expr) ?? "_";
			var body = functionBody.Get (expr);
			var ftype = expr;
			var rtype = FunctionReturnType.Get (ftype);
			var fargCs = FunctionArguments.Get (ftype).Select (arg => GetCSType (ArgumentType.Get (arg))).ToArray ();
			MethodBuilder fn = tb.DefineMethod (name, MethodAttributes.Static | MethodAttributes.Public,
				                   GetCSType (rtype), fargCs);
			FunctionInvocation [expr] = fn;
			var ilgen = fn.GetILGenerator ();
			Interact.Load (this, ilgen);
			Uid rt;
			using (localSymbols.WithValue (new Dict<Uid, LocalSymData> ())) {
				var fargs = FunctionArguments.Get (expr);
				short paramIndex = 0;
				foreach (var arg in fargs) {
					var argname = ArgumentName.Get (arg) ?? ("arg_" + arg);
					fn.DefineParameter (paramIndex + 1, ParameterAttributes.None, argname);
					localSymbols.Value.Add (arg, new LocalSymData{ ArgIndex = paramIndex, TypeId = ArgumentType.Get (arg) });
					paramIndex += 1;
				}
				rt = CompileSubExpression (body);
			}
			if (rtype != VoidType && rt != rtype)
				throw new Exception ("Return types does not match");
			if (rtype == VoidType && rt != VoidType) {
				ilgen.Emit (OpCodes.Pop);
			} 
			ilgen.Emit (OpCodes.Ret);
			Type t = tb.CreateType ();
			return FunctionInvocation [expr] = t.GetMethod (fn.Name);   
		}

		public delegate Uid SubExpressionDelegate (params Uid[] uids);

		public SubExpressionDelegate Sub { get { return Expression; } }

		public Uid Expression (params Uid[] uids)
		{
			var uid = Uid.CreateNew ();
			Expressions.Add (uid);
			SubExpressions.Add (uid, uids);
			return uid;
		}

		public Dict<Uid, string> ArgumentName = new Dict<Uid, string> ();
		public Dict<Uid, Uid> ArgumentType = new Dict<Uid, Uid> ();

		public Uid DefineArgument (string name, Uid type)
		{
			var id = Uid.CreateNew ();
			if (name != null)
				ArgumentName.Add (id, name);
			ArgumentType.Add (id, type);
			return id;
		}

		public Uid Arg (string name, Uid type)
		{
			return DefineArgument (name, type);
		}

		// Variable that defines the struct name.
		Dict<Uid, Uid> structName = new Dict<Uid, Uid> ();
		MultiDict<Uid, Uid> structMembers = new MultiDict<Uid, Uid> ();

		public Uid DefineStruct (Uid nameVar = default(Uid), params Uid[] arguments)
		{
			var id = Uid.CreateNew ();

			if (nameVar != default(Uid))
				structName.Add (id, nameVar);
			structMembers.Add (id, arguments);
			types.Add (id, Types.Struct);
			return id;
		}

		Uid getStructAccess;

		public Uid GetStructAccessor (Uid member, Uid structexpr)
		{
			var structid = structMembers.Entries.FirstOrDefault (e => e.Value.Contains (member)).Key;
			return Sub (getStructAccess, structid, member, structexpr);
		}

		HashSet<Uid> ReferenceReq = new HashSet<Uid> ();

		public static Uid genStructAccess (Uid expr, IlGen gen)
		{
			var sexprs = gen.SubExpressions.Get (expr);
			var structid = sexprs [1];
			var memberid = sexprs [2];
			FieldInfo field = gen.getNetFieldInfo (memberid, structid);
			Uid valueExpr = gen.SetExprs.Get (expr);
            
			if (valueExpr != Uid.Default) {
				gen.SetExprs.Remove (expr);
				gen.ReferenceReq.Add (sexprs [3]);
				gen.CompileSubExpression (sexprs [3]);
				if (gen.ReferenceReq.Contains (sexprs [3]))
					throw new CompilerError (expr, "Unable to access right valaue");
				Interact.Emit (OpCodes.Dup);
				gen.CompileSubExpression (valueExpr);
				Interact.Emit (OpCodes.Stfld, field);
				Interact.Emit (OpCodes.Ldfld, field);
                
			} else {
				gen.CompileSubExpression (sexprs [3]);
				Interact.Emit (OpCodes.Ldfld, field);
			}

			return gen.ArgumentType.Get (memberid);
		}

		Uid InitStruct;

		public static Uid GenStruct (Uid expr, IlGen gen)
		{
			var subexprs = gen.SubExpressions.Get (expr);
			gen.GenStructConstructorIl (subexprs [1]);
			return subexprs [1];
		}

		public Uid[] GetStructConstructor (Uid struct_id)
		{
			if (InitStruct == Uid.Default) {
				InitStruct = Uid.CreateNew ();
				Macros.Add (InitStruct, GenStruct);
			}

			return new[] { InitStruct, struct_id };
		}

		void GenStructConstructorIl (Uid structid)
		{
			Interact.Emit (OpCodes.Ldloc, Interact.DeclareLocal (GetCSType (structid)));
		}

		FieldInfo getNetFieldInfo (Uid member, Uid structType)
		{
			var args = structMembers.Get (structType);
			var idx = args.IndexOf (member);
			return GetCSType (structType).GetFields () [idx];
		}

		public Uid DefineFunction (string name, Uid returnType, params Uid[] arguments)
		{

			var id = Uid.CreateNew ();
			FunctionReturnType.Add (id, returnType);
			FunctionArguments.Add (id, arguments);
			FunctionName.Add (id, name);
			return id;
		}



		public void DefineFcnBody (Uid fcn, Uid body)
		{
			functionBody.Add (fcn, body);
		}

		public readonly Uid Progn;

		public Uid genProgn (Uid expr, IlGen gen)
		{
			var exprs = gen.SubExpressions.Get (expr);
			if (exprs.Count == 1)
				return VoidType;
			if (exprs.Count == 2) {
				return CompileSubExpression (exprs [1]);
			}
			for (int i = 1; i < exprs.Count; i++) {
				Uid type = CompileSubExpression (exprs [i]);
				if (i == exprs.Count - 1) {
					return type;
				} else if (type != VoidType) {
					Interact.Emit (OpCodes.Pop);
				}
			}
			Debug.Fail ("Unreachable");
			return Uid.Default;
		}

		public Uid genAdd (OpCode opcode, Uid expr, IlGen gen)
		{
			var subs = gen.SubExpressions.Get (expr);
			if (subs.Count < 2)
				throw new CompilerError (expr, "Invalid number of arguments for +");
			Uid type = CompileSubExpression (subs [1]);
			for (int i = 2; i < subs.Count; i++) {
				Uid t2 = CompileSubExpression (subs [i]);
				if (type != t2)
					throw new CompilerError (subs [i], "Invalid type for +");
				Interact.Emit (opcode);
			}
			return type;
		}

		Dict<OpCode, Uid> BaseOpCodes = new Dict<OpCode, Uid> ();
		public readonly Dict<Uid, string> MacroNames = new Dict<Uid, string> ();

		public Uid genBaseFunctor (OpCode c, string name)
		{
			if (BaseOpCodes.ContainsKey (c) == false) {
				Uid id = Uid.CreateNew ();
				BaseOpCodes.Add (c, id);
				Macros.Add (id, (x, z) => genAdd (c, x, z));
				MacroNames.Add (id, name);

			}
			return BaseOpCodes.Get (c);
		}

		public readonly Uid Let;

		public readonly Uid Set;

		//(setf (member-x sym) 5)
		// vs
		//(set-member-x sym 5)
		// Setf needs to communicate with whatever the inner form is for that
		// to work.
		public Dict<Uid,Uid> SetExprs = new Dict<Uid,Uid> ();


		public Uid genSet (Uid expr, IlGen gen)
		{
			var exprs = SubExpressions.Get (expr);
			// set, accessor, value
			SetExprs.Add (exprs [1], exprs [2]);
			Uid typeid2 = CompileSubExpression (exprs [1]);
			if (SetExprs.ContainsKey (exprs [1]))
				throw new CompilerError (expr, "Sub expression does not support set");
			return typeid2;
		}

		struct LocalSymData
		{
			public LocalBuilder Local;
			public short ArgIndex;
			public Uid TypeId;
		}

		StackLocal<Dict<Uid, LocalSymData>> localSymbols = new StackLocal<Dict<Uid, LocalSymData>> (new Dict<Uid, LocalSymData> ());

		public Uid genLet (Uid expr, IlGen gen)
		{
			var exprs = gen.SubExpressions.Get (expr);
			Uid type = CompileSubExpression (exprs [2]);
			var local = Interact.DeclareLocal (GetCSType (type));
			Interact.Emit (OpCodes.Dup);
			Interact.Emit (OpCodes.Stloc, local);
			localSymbols.Value.Add (exprs [1], new LocalSymData { Local = local, TypeId = type });
			return type;
		}

		public Uid Sym (string name = null)
		{
			if (name != null && SymbolNames.ContainsKey (name)) {
				return SymbolNames [name];
			}
			var @new = Uid.CreateNew ();
			if (name != null) {
				SymbolNames [name] = @new;
			}
			Symbols.Add (@new);
			return @new;
		}

		public bool IsSym (Uid id) => Symbols.Contains(id);

		public string SymName (Uid id) => SymbolNames.FirstOrDefault(x => x.Value == id).Key;

		public void AddMacro (Uid id, MacroDelegate d, string macroName = null)
		{
			userMacros.Add (id, d);
			macroName = macroName ?? SymbolNames.FirstOrDefault (x => x.Value == id).Key;
			if (macroName != null)
				MacroNames.Add (id, macroName);
		}

		public void AddMacro (Uid id, MethodInfo m)
		{
			Assert.IsTrue (m.IsStatic && m.IsPublic);
			userMacros.Add (id, expr => (Uid)m.Invoke (null, null));
			MacroNames.Add (id, m.Name);
		}


		public void AddMacroSpec (Uid macro, MacroSpecDelegate func)
		{
			macroSpecs.Add (macro, func);
		}

		public MacroSpecDelegate GetMacroSpec (Uid macro)
		{
			return macroSpecs.Get (macro);
		}

		public Uid GetFunctionBody (Uid fcn)
		{
			return functionBody.Get (fcn);
		}

		[ProtoBuf.ProtoContract]
		public class Serialized{
			[ProtoBuf.ProtoMember(1)]
			public string TypeName;
			[ProtoBuf.ProtoMember(2)]
			public object Data;
		}

		// Pack all the data needed. Most importantly, the types and methods generated. 
		public void Save(string filepath){
			builder.Save (AssemblyName ());
			File.Delete (filepath);
			using (var str = File.OpenWrite (filepath)) {
				var bytes = File.ReadAllBytes (AssemblyName ());
				Serializer.SerializeWithLengthPrefix (str, bytes, PrefixStyle.Base128,1);
				foreach (var mod in modules) {
					var serial = new Serialized{ TypeName = mod.Key.FullName, Data = mod.Value.Save () };
					if (serial.Data != null) {
						Serializer.SerializeWithLengthPrefix (str, serial.TypeName, PrefixStyle.Base128,2);
						Serializer.SerializeWithLengthPrefix (str, serial.Data.GetType().FullName, PrefixStyle.Base128,3);
						Serializer.NonGeneric.SerializeWithLengthPrefix (str, serial.Data, PrefixStyle.Base128,4);
					}
				}
			}
		}

		public void Load(string filepath){
			
			byte[] bytedata = null;
			using (var str = File.OpenRead (filepath)) {
				bytedata = ProtoBuf.Serializer.DeserializeWithLengthPrefix<byte[]> (str, PrefixStyle.Base128,1);
				Assembly asm = null;
				{
					var rasm = Assembly.ReflectionOnlyLoad (bytedata);
					var guid = Guid.Parse (rasm.GetCustomAttribute<GuidAttribute> ().Value);
					var asms = AppDomain.CurrentDomain.GetAssemblies ();
					asm = asms.FirstOrDefault (a => a.FullName == rasm.FullName);
					if (loadedAssemblies.Contains (guid))
						return;
				}

				asm = asm ?? Assembly.Load (bytedata);
				Uid.AssemblyIdStore.Value = 1;
				while (str.Position + 1 <= str.Length) {
					var s = ProtoBuf.Serializer.DeserializeWithLengthPrefix<string> (str, PrefixStyle.Base128,2);
					var s2 = ProtoBuf.Serializer.DeserializeWithLengthPrefix<string> (str, PrefixStyle.Base128,3);
					var tp = System.Type.GetType (s);
					var tp2 = System.Type.GetType (s2);
					object obj;
					bool ok = ProtoBuf.Serializer.NonGeneric.TryDeserializeWithLengthPrefix(str, PrefixStyle.Base128, fld => tp2, out obj);
					Assert.IsTrue (ok);
					GetModule (tp).LoadData (asm, obj);
				}
				Uid.AssemblyIdStore.Value = 0;
			}
		}
	}
}