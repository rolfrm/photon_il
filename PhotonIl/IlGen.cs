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
using System.IO.Compression;

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

	public class MacroAttribute : Attribute{
		public string Name;
		public string SpecName;
		public string Printer;
		public MacroAttribute(string name, string specName = null, string printer = null){
			Name = name;
			SpecName = specName;
			Printer = printer;
		}
	}

	public class IlGen : IPhotonModule
	{
		public BaseFunctions F { get { return GetModule<BaseFunctions> (); } }
		public ArrayModule A { get { return GetModule<ArrayModule> (); } }

		public Dict<Uid, Types> types = new Dict<Uid, Types> ();
		public Dict<Uid, string> type_name = new Dict<Uid, string> ();

		public Dict<Uid, string> ArgumentName = new Dict<Uid, string> ();
		public Dict<Uid, Uid> ArgumentType = new Dict<Uid, Uid> ();

		public Dict<Uid, Uid> FunctionReturnType = new Dict<Uid, Uid> ();
		public Dict<Uid, MethodInfo> FunctionInvocation = new Dict<Uid, MethodInfo> ();
		public Dict<Uid, string> FunctionName = new Dict<Uid, string> ();
		public MultiDict<Uid, Uid> FunctionArguments = new MultiDict<Uid, Uid> ();
		public Dict<Uid, Uid> functionBody = new Dict<Uid, Uid> ();

		public Dict<Uid, MethodInfo> Macros = new Dict<Uid, MethodInfo> ();
		public Uid U8Type { get { return type_name.Inv("u8");}}
		public Uid U16Type { get { return type_name.Inv("u16");}}
		public Uid U32Type { get { return type_name.Inv("u32");}}
		public Uid I32Type { get { return type_name.Inv("i32");}}
		public Uid F32Type { get { return type_name.Inv("f32");}}
		public Uid F64Type { get { return type_name.Inv("f64");}}
		public Uid VoidType { get { return type_name.Inv("void");}}
		public Uid StringType { get { return type_name.Inv("string");}}
		public Uid UidType { get { return type_name.Inv("uid");}}

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
		//Dict<int,Assembly> loadedAssemblyId = new Dict<int, Assembly>();

		public Uid Add { get { return SymbolNames ["+"]; } }
		public Uid Subtract { get { return SymbolNames ["-"]; } }
		public Uid Multiply { get { return SymbolNames ["*"]; } }
		public Uid Divide { get { return SymbolNames ["/"]; } }

		Dict<string, Uid> SymbolNames = new Dict<string, Uid> ();
		HashSet<Uid> Symbols = new HashSet<Uid> ();
		Dict<Uid,MacroDelegate> userMacros = new Dict<Uid, MacroDelegate> ();

		public delegate Uid MacroDelegate (Uid expr);
		public delegate Uid MacroSpecDelegate (Uid expr, int index, string suggestion);

		Dict<Uid,MethodInfo> macroSpecs = new Dict<Uid, MethodInfo> ();

		[ProtoContract]
		public struct Savable{
			[ProtoMember(1)]
			public Dict<Uid, string> TypeName;

			[ProtoMember(2)]
			public Dict<Uid, string> CsTypeNames;

			[ProtoMember(3)]
			public Dict<Uid, Tuple<string, string>> FuncNames;

			[ProtoMember(4)]
			public Dict<Uid, string> FunctionNames;

			[ProtoMember(5)]
			public Dict<Uid, Tuple<string, string>> Macros;

			[ProtoMember(6)]
			public Dict<Uid, string> MacroNames;

			[ProtoMember(7)]
			public MultiDict<Uid, Uid> SubExpressions;

			[ProtoMember(8)]
			public Dict<Uid,Uid> FunctionReturnType;

			[ProtoMember(9)]
			public MultiDict<Uid,Uid> FunctionArguments;

			[ProtoMember(10)]
			public Dict<Uid, string> ArgumentName;

			[ProtoMember(11)]
			public Dict<Uid, Uid> ArgumentType;

			[ProtoMember(12)]
			public Dict<Uid, Tuple<string, string>> MacroSpecs;

			[ProtoMember(13)]
			public Dict<Uid, Tuple<string, string>> Printers;
		};

		public void Load(IlGen gen){

		}

		public void LoadData(Assembly asm, object serialized){
			var savable = (Savable)serialized;
			if(savable.TypeName != null)
			foreach (var x in savable.TypeName)
				type_name [x.Key] = x.Value;
			if(savable.CsTypeNames != null)
			foreach (var x in savable.CsTypeNames)
				generatedStructs [x.Key] = asm == null ? Type.GetType(x.Value) :  asm.GetType (x.Value);
			if (savable.FuncNames != null)
				foreach (var x in savable.FuncNames) {
                    Type basetype = asm?.GetType(x.Value.Item1.Split(',')[0]) ?? Type.GetType(x.Value.Item1);
					FunctionInvocation [x.Key] = basetype.GetMethod (x.Value.Item2, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				}
			if (savable.FunctionNames != null)
				foreach (var x in savable.FunctionNames) {
					FunctionName [x.Key] = x.Value;
				}
			if(savable.Macros != null)
			foreach (var x in savable.Macros)
				Macros [x.Key] = (asm == null ? Type.GetType(x.Value.Item1) : asm.GetType (x.Value.Item1)).GetMethod (x.Value.Item2);
			if(savable.MacroNames != null)
			foreach (var x in savable.MacroNames) {
				MacroNames [x.Key] = x.Value;
				SymbolNames [x.Value] = x.Key;
			}
			if(savable.SubExpressions != null)
				foreach (var x in savable.SubExpressions.Entries)
					if(x.Value != null)
						SubExpressions.Add(x.Key, x.Value);
			if(savable.FunctionReturnType != null)
				foreach (var x in savable.FunctionReturnType)
					FunctionReturnType.Add(x.Key, x.Value);
			if(savable.FunctionArguments != null)
				foreach (var x in savable.FunctionArguments.Entries)
					if(x.Value != null)
						FunctionArguments.Add(x.Key, x.Value);
			if(savable.ArgumentName != null)
				foreach (var x in savable.ArgumentName)
					ArgumentName.Add(x.Key, x.Value);
			if(savable.ArgumentType != null)
				foreach (var x in savable.ArgumentType)
					ArgumentType.Add(x.Key, x.Value);
			if(savable.MacroSpecs != null)
				foreach(var x in savable.MacroSpecs){
					Type basetype = (asm == null ? Type.GetType (x.Value.Item1) : (asm.GetType (x.Value.Item1) ?? Type.GetType (x.Value.Item1)));
					macroSpecs[x.Key] = basetype.GetMethod (x.Value.Item2, BindingFlags.Static | BindingFlags.Public);
					}
			if(savable.Printers != null)
				foreach(var x in savable.Printers){
					Type basetype = (asm == null ? Type.GetType (x.Value.Item1) : (asm.GetType (x.Value.Item1) ?? Type.GetType (x.Value.Item1)));
					Printers[x.Key] = basetype.GetMethod (x.Value.Item2, BindingFlags.Static | BindingFlags.Public);
				}
			
		}
		public object Save(){

			return new Savable {
				TypeName = type_name.LocalOnly(),
				CsTypeNames = generatedStructs.LocalOnly().ConvertValues(x => x.FullName),
				FuncNames = this.FunctionInvocation.LocalOnly().ConvertValues(x => Tuple.Create(x.DeclaringType.AssemblyQualifiedName, x.Name)),
				Macros = this.Macros.LocalOnly().ConvertValues(x => Tuple.Create(x.DeclaringType.AssemblyQualifiedName, x.Name)),
				MacroNames = this.MacroNames.LocalOnly(),
				FunctionNames = this.FunctionName.LocalOnly(),
				SubExpressions = this.SubExpressions.LocalOnly(),
				FunctionReturnType = this.FunctionReturnType.LocalOnly(),
				FunctionArguments = this.FunctionArguments.LocalOnly(),
				ArgumentName = this.ArgumentName.LocalOnly(),
				ArgumentType = this.ArgumentType.LocalOnly(),
				MacroSpecs = this.macroSpecs.LocalOnly().ConvertValues(x => Tuple.Create(x.DeclaringType.AssemblyQualifiedName, x.Name)),
				Printers = this.Printers.LocalOnly().ConvertValues(x => Tuple.Create(x.DeclaringType.AssemblyQualifiedName, x.Name)),

			};

		}

		public bool IsBare = false;

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


		public Uid AddPrimitive (string name, Type dotNetType)
		{
			var prim = Uid.CreateNew ();
			types.Add (prim, Types.Primitive);
			type_name.Add (prim, name);
			generatedStructs.Add (prim, dotNetType);
			return prim;
		}

		Uid addCSharpType (Type type, string name = null)
		{
			if (type.IsValueType == false)
				throw new Exception ("Unable to add non-value type");

			Uid uid = Uid.CreateNew ();

			this.types.Add (uid, Types.Struct);
			this.generatedStructs.Add (uid, type);
			this.type_name.Add (uid, name ?? type.Name);
			return uid;
		}

		public IlGen (bool bare = false)
		{
			modules [typeof(IlGen)] = this;
			IsBare = bare;
			if (bare)
				return;
			AddPrimitive ("u8", typeof(byte));
			AddPrimitive ("u16", typeof(short));
			AddPrimitive ("u32", typeof(uint));
			AddPrimitive ("i32", typeof(int));
			AddPrimitive ("f32", typeof(float));
			AddPrimitive ("f64", typeof(double));

			AddPrimitive ("void", typeof(void));
			AddPrimitive ("string", typeof(string));
			addCSharpType (typeof(Uid), "uid");

			foreach (var m in GetType().GetMethods(BindingFlags.Public | BindingFlags.Static)) {
				
				var attr = m.GetCustomAttribute<MacroAttribute> ();
				if (attr == null)
					continue;
				var id = Sym (attr.Name);
				Macros.Add (id, m);
				MacroNames.Add (id, attr.Name);
				if (attr.SpecName != null)
					AddMacroSpec (id, this.GetType (), attr.SpecName);
				if (attr.Printer != null)
					AddPrinter (id, this.GetType (), attr.Printer);
			}

			GetModule<BaseFunctions> ();
			GetModule<ArrayModule> ();
		}

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
            if (compileIntercept != null)
                if (!compileIntercept(expr))
                    return VoidType;
			{
				var constantType = ConstantType.Get (expr);
				if (constantType != Uid.Default) {
					if (this.types.Get (constantType) == Types.Primitive) {
						
						if (constantType == F32Type)
							Interact.Emit (OpCodes.Ldc_R4, (float)Convert.ChangeType (ConstantValue.Get (expr), typeof(float)));
						else if (constantType == F64Type)
							Interact.Emit (OpCodes.Ldc_R8, (double)Convert.ChangeType (ConstantValue.Get (expr), typeof(double)));
						else if (constantType == I32Type || constantType == U32Type || constantType == U8Type || constantType == U16Type)
							Interact.Emit (OpCodes.Ldc_I4, (int)Convert.ChangeType (ConstantValue.Get (expr), typeof(int)));
						// TODO implement U8,U16,...
						//else if (constantType == U64Type)
						//	Interact.Emit (OpCodes.Ldc_I8, (long)Convert.ChangeType (ConstantValue.Get (expr), typeof(long)));
						else if (constantType == StringType) {
							Interact.Emit (OpCodes.Ldstr, (string)Convert.ChangeType (ConstantValue.Get (expr), typeof(string)));
						}	
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
				if (gen != null) {
					try{
					return (Uid)gen.Invoke (null, new object[]{ expr });
					}catch(TargetInvocationException te){
						throw te.InnerException;
					}

				}
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
				if (functionBody.Get (expr) == Uid.Default)
					throw new CompilerError (expr, "Not a function");
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
					throw new CompilerError (subexprs [i], "Invalid type of arg {0}. Expected {1}, got {2}.", i - 1, ArgumentType.Get (args [i - 1]), type);
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

        public Uid GenExpression(Uid expr, params Uid[] arguments)
        {
            using (Interact.Push(this, null))
            {
                Uid rt;
                using (localSymbols.WithValue(new Dict<Uid, LocalSymData>()))
                {

                    short paramIndex = 0;
                    foreach (var arg in arguments)
                    {
                        localSymbols.Value.Add(arg, new LocalSymData { ArgIndex = paramIndex, TypeId = ArgumentType.Get(arg) });
                        paramIndex += 1;
                    }
                    rt = CompileSubExpression(expr);
                }
                return rt;
            }
		}
		int typeid = 0;
        public MethodInfo GenerateIL(Uid expr)
        {
			ModuleBuilder module = getDynamicModule();
            var tb = module.DefineType($"MyType{typeid++}", TypeAttributes.Class | TypeAttributes.Public);

            var name = FunctionName.Get(expr) ?? "_";
            var body = functionBody.Get(expr);
            var ftype = expr;
            var rtype = FunctionReturnType.Get(ftype);
            var fargCs = FunctionArguments.Get(ftype).Select(arg => GetCSType(ArgumentType.Get(arg))).ToArray();
            MethodBuilder fn = tb.DefineMethod(name, MethodAttributes.Static | MethodAttributes.Public,
                                   GetCSType(rtype), fargCs);
            FunctionInvocation[expr] = fn;
            var ilgen = fn.GetILGenerator();
            using (Interact.Push(this, ilgen))
            {
                Uid rt;
                using (localSymbols.WithValue(new Dict<Uid, LocalSymData>()))
                {
                    var fargs = FunctionArguments.Get(expr);
                    short paramIndex = 0;
                    foreach (var arg in fargs)
                    {
                        var argname = ArgumentName.Get(arg) ?? ("arg_" + arg);
                        fn.DefineParameter(paramIndex + 1, ParameterAttributes.None, argname);
                        localSymbols.Value.Add(arg, new LocalSymData { ArgIndex = paramIndex, TypeId = ArgumentType.Get(arg) });
                        paramIndex += 1;
                    }
                    rt = CompileSubExpression(body);
                }
                if (rtype != VoidType && rt != rtype)
                    throw new Exception("Return types does not match");
                if (rtype == VoidType && rt != VoidType)
                {
                    ilgen.Emit(OpCodes.Pop);
                }
                ilgen.Emit(OpCodes.Ret);
                Type t = tb.CreateType();
                return FunctionInvocation[expr] = t.GetMethod(fn.Name);
            }
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

		public Uid GetStructAccessor (Uid member, Uid structexpr)
		{
			var structid = structMembers.Entries.FirstOrDefault (e => e.Value.Contains (member)).Key;
			return Sub (SymbolNames["struct access"], structid, member, structexpr);
		}

		HashSet<Uid> ReferenceReq = new HashSet<Uid> ();

		[Macro("struct access")]
		public static Uid genStructAccess (Uid expr)
		{
			var gen = Interact.Current;
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

		[Macro("init struct")]
		public static Uid GenStruct (Uid expr)
		{
			var subexprs = Interact.Current.SubExpressions.Get (expr);
			Interact.Current.GenStructConstructorIl (subexprs [1]);
			return subexprs [1];
		}

		public Uid[] GetStructConstructor (Uid struct_id)
		{
			return new[] { SymbolNames["init struct"], struct_id };
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

		public Uid DefineFunction (Uid id, string name, Uid returnType, params Uid[] arguments)
		{
			if (id == Uid.Default)
				id = Uid.CreateNew ();
			FunctionReturnType[id] = returnType;
			FunctionArguments.Remove (id);
			FunctionArguments.Add(id, arguments);
			FunctionName[id] = name;
			return id;
		}

		public Uid DefineFunction (string name, Uid returnType, params Uid[] arguments)
		{
			return DefineFunction (Uid.CreateNew (), name, returnType, arguments);
		}

		public void DefineFcnBody (Uid fcn, Uid body)
		{
			functionBody[fcn] = body;
		}

		public Uid Progn { get { return SymbolNames ["progn"];}}

		[Macro("progn")]
		public static Uid genProgn (Uid expr)
		{
			var exprs = Interact.Current.SubExpressions.Get (expr);
			if (exprs.Count == 1)
				return Interact.Current.VoidType;
			if (exprs.Count == 2) {
				return Interact.Current.CompileSubExpression (exprs [1]);
			}
			for (int i = 1; i < exprs.Count; i++) {
				Uid type = Interact.Current.CompileSubExpression (exprs [i]);
				if (i == exprs.Count - 1) {
					return type;
				} else if (type != Interact.Current.VoidType) {
					Interact.Emit (OpCodes.Pop);
				}
			}
			Assert.Fail ("Unreachable");
			return Uid.Default;
		}

		public Uid genbase (OpCode opcode, Uid expr)
		{
			var subs = SubExpressions.Get (expr);
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

		[Macro("+", nameof(genAddSpec))]
		public static Uid genAdd(Uid expr){
			return Interact.Current.genbase (OpCodes.Add, expr);
		}

		public static Uid genAddSpec(Uid expr, int index, string suggestion){
			if (string.IsNullOrEmpty (suggestion))
				return Uid.Default;
			var exprs = Interact.Current.SubExpressions.Get (expr);;
			if (index == 0)
				return exprs [index];
			else {
				var checkexpr = exprs.Skip(1).FirstOrDefault(x => x != Uid.Default);
				if (checkexpr == Uid.Default)
					return checkexpr;
				try{
					
					Uid ret = Interact.Current.GenExpression (checkexpr, Interact.CurrentArgs.ToArray() );
					return ret;
				}catch{

				}
			}
			
			return Uid.Default;
		}

		[Macro("-", nameof(genAddSpec))]
		public static Uid genSub(Uid expr){
			return Interact.Current.genbase (OpCodes.Sub, expr);
		}

		[Macro("*", nameof(genAddSpec))]
		public static Uid genMul(Uid expr){
			return Interact.Current.genbase (OpCodes.Mul, expr);
		}

		[Macro("/", nameof(genAddSpec))]
		public static Uid genDiv(Uid expr){
			return Interact.Current.genbase (OpCodes.Div, expr);
		}

		public readonly Dict<Uid, string> MacroNames = new Dict<Uid, string> ();

		public Uid Let { get { return SymbolNames ["let"];}}

		public Uid Set { get { return SymbolNames ["set"];}}

		//(setf (member-x sym) 5)
		// vs
		//(set-member-x sym 5)
		// Setf needs to communicate with whatever the inner form is for that
		// to work.
		public Dict<Uid,Uid> SetExprs = new Dict<Uid,Uid> ();

		[Macro("set")]
		public static Uid genSet (Uid expr)
		{
			var gen = Interact.Current;
			var exprs = gen.SubExpressions.Get (expr);
			if (exprs.Count == 0)
				return Uid.Default;
			// set, accessor, value
			gen.SetExprs[exprs [1]] = exprs.Count == 3 ? exprs [2] : Uid.Default;
			Uid typeid2 = gen.CompileSubExpression (exprs [1]);
			if (gen.SetExprs.ContainsKey (exprs [1])) {
				gen.SetExprs.Remove (exprs [1]);
				throw new CompilerError (expr, "Sub expression does not support set");
			}
			return typeid2;
		}

		struct LocalSymData
		{
			public LocalBuilder Local;
			public short ArgIndex;
			public Uid TypeId;
		}

		public FieldInfo DefineGlobal(string name, Type type){
			var module = getDynamicModule ();
			var tb = module.DefineType($"MyType{typeid++}", TypeAttributes.Class | TypeAttributes.Public);
			var field = tb.DefineField (name, type, FieldAttributes.Static | FieldAttributes.Public);
			tb.CreateType ();
			return field;
		}

		Dict<Uid, FieldInfo> globals = new Dict<Uid, FieldInfo>();
		Dict<Uid, string> globalName = new Dict<Uid, string>();
		Dict<Uid, Uid> globalType = new Dict<Uid, Uid>();
		[Macro("global", printer: nameof(printGlobal))]
		public static Uid defineGlobal(Uid expr){
			var gen = Interact.Current;
			var exprs = gen.SubExpressions.Get (expr);
			if (exprs.Count != 3)
				throw new CompilerError (expr, "Invalid number of arguments. Expected 2, got {0}.", exprs.Count);
			if (Interact.IsDryRun)
				return gen.VoidType;
			
			Uid type = gen.CompileSubExpression (exprs [2]);
			Uid nameid = exprs [1];
			var id = Uid.CreateNew ();
			var name = (string)gen.ConstantValue.Get (exprs [1]);

			if (gen.globals.Get (id)?.FieldType != gen.GetCSType (type)){
				var field = gen.DefineGlobal (name, gen.GetCSType (type));
				gen.globals[id] = field;
				gen.globalName [id] = name;
				gen.globalType [id] = type;
			}

			Interact.Emit (OpCodes.Stsfld, gen.globals.Get (id));
			return gen.VoidType;
		}

		[Macro("glob", nameof(globSpec))]
		public static Uid getGlobal(Uid expr){
			// (set (glob x) 5)
			// (print (glob x))
			var gen = Interact.Current;
			Uid valueExpr = gen.SetExprs.Get(expr);
			var s = gen.SubExpressions.Get (expr);
			FieldInfo field = gen.globals[s[1]];
			Uid type = gen.globalType.Get (s[1]);
			if (valueExpr != Uid.Default) {
				Uid retType = Interact.CallOn (valueExpr);
				if (retType != type)
					throw new CompilerError (expr, "Types does not match!");
				Interact.Emit (OpCodes.Dup);
				Interact.Emit (OpCodes.Stsfld, field);
				gen.SetExprs.Remove (expr);
			} else {
				Interact.Emit (OpCodes.Ldsfld, field);
			}
			return type;
		}

		public static string printGlobal(Uid id){
			return Interact.Current.globalName.Get (id);
		}

		public static Uid[] globSpec(Uid expr, int index, string suggestion){
			if (index > 1)
				throw new CompilerError (expr, "Only 2 arguments to glob are supported");
			var gen = Interact.Current;
			var s = gen.SubExpressions.Get (expr);

			if (index == 1 && suggestion != null)
				return gen.globalName.Where(x => x.Value.StartsWith(suggestion)).Select(x => x.Key).ToArray();
			return new Uid[0];
		}

		StackLocal<Dict<Uid, LocalSymData>> localSymbols = new StackLocal<Dict<Uid, LocalSymData>> (new Dict<Uid, LocalSymData> ());
		[Macro("let")]
		public static Uid genLet (Uid expr)
		{
			var gen = Interact.Current;
			var exprs = gen.SubExpressions.Get (expr);
			Uid type = gen.CompileSubExpression (exprs [2]);
			var local = Interact.DeclareLocal (gen.GetCSType (type));
			Interact.Emit (OpCodes.Dup);
			Interact.Emit (OpCodes.Stloc, local);
			gen.localSymbols.Value.Add (exprs [1], new LocalSymData { Local = local, TypeId = type });
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

		public void AddMacro (Uid id, MethodInfo m, string name = null)
		{
			name = name ?? SymName(id) ?? m.Name;
			Assert.IsTrue (m.IsStatic && m.IsPublic);
			Macros.Add(id, m);
			MacroNames.Add (id, name);
		}

		public void AddMacro(Uid macro, Type declaringType, string methodName){
			var m = declaringType.GetMethod (methodName, BindingFlags.Static | BindingFlags.Public);
			Assert.IsTrue (m != null);
			AddMacro (macro, m);
		}


		public void AddMacroSpec(Uid macro, Type declaringType, string methodName){
			var m = declaringType.GetMethod (methodName, BindingFlags.Static | BindingFlags.Public);
			Assert.IsTrue (m != null);
			AddMacroSpec (macro, m);
		}

		public void AddMacroSpec (Uid macro, MethodInfo func)
		{
			macroSpecs.Add (macro, func);
		}

		public void AddPrinter(Uid macro, Type declaringType, string methodName){
			var m = declaringType.GetMethod (methodName, BindingFlags.Static | BindingFlags.Public);
			Assert.IsTrue (m != null);
			AddPrinter (macro, m);
		}

		public void AddPrinter(Uid macro,MethodInfo method){
			Printers[macro] = method;
		}

		public Dict<Uid,MethodInfo> Printers = new Dict<Uid, MethodInfo>();

        event Func<Uid, bool> compileIntercept;

        public Uid[] InvokeMacroSpec(Uid baseexpr, Uid expr, int index, string str)
        {
            var sub = SubExpressions.Get(expr);
            if ( 0 == sub.Count || false == macroSpecs.ContainsKey(sub[0]))
				return new Uid[]{};
            var spec = macroSpecs.Get(sub[0]);
			Uid[] result = new Uid[0];
            Func<Uid, bool> checkf = x =>
            {
                if (x == expr)
                {
                    try
                    {
                        object result2 = spec.Invoke(null, new object[] { expr, index, str });
						if(result2 is Uid[]){
							result = (Uid[]) result2;
						}
						else if (result2 is Uid && ((Uid) result2) != Uid.Default){
							
							result = new []{(Uid)result2};
						}else{
							throw new CompilerError(baseexpr, "Unsupported type {0}.", result2?.GetType());
						}
                    }
                    catch { }
                    return false;
                }
                return true;
            };
            compileIntercept += checkf;
            try
            {
                GenExpression(baseexpr);
            }
            catch { }

            compileIntercept -= checkf;
            return result;
        }

		public MacroSpecDelegate GetMacroSpec (Uid macro)
		{
			if (macroSpecs.Get (macro) == null)
				return null;
			return new MacroSpecDelegate((Uid x, int y, string z) => (Uid)macroSpecs.Get (macro).Invoke (null, new Object[]{ x, y, z }));
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
            if (builder != null)
                builder.Save(AssemblyName());
			File.Delete (filepath);
			using (var str = new GZipStream(File.OpenWrite (filepath), CompressionMode.Compress,false)) {
				var bytes = builder == null ? new byte[0] : File.ReadAllBytes (AssemblyName ());
				Serializer.SerializeWithLengthPrefix (str, bytes, PrefixStyle.Base128,1);
				Serializer.SerializeWithLengthPrefix (str, loadedBins, PrefixStyle.Base128,2);
				int fieldIndex = 3;
				foreach (var mod in modules) {
					var data = mod.Value.Save ();
					if (data == null)
						continue;
					var typename = mod.Key.FullName;
					var dataTypeName = data.GetType ().FullName;
					Serializer.SerializeWithLengthPrefix (str, typename, PrefixStyle.Base128,fieldIndex++);
					Serializer.SerializeWithLengthPrefix (str, dataTypeName, PrefixStyle.Base128,fieldIndex++);
					Serializer.NonGeneric.SerializeWithLengthPrefix (str, data, PrefixStyle.Base128,fieldIndex++);
				}
			}
            builder = null; // Builder must not be used again due to restriction in (MS).NET
        }
		Dict<string, int> loadedBins = new Dict<string, int>();
		static int assemblyStoreId = 1;
		public void Load(string filepath, bool import = false){
			
			byte[] bytedata = null;
			using (var str = new GZipStream(File.OpenRead (filepath), CompressionMode.Decompress,false)) {
				bytedata = ProtoBuf.Serializer.DeserializeWithLengthPrefix<byte[]> (str, PrefixStyle.Base128,1);
				var assemblyloadIds = ProtoBuf.Serializer.DeserializeWithLengthPrefix<Dict<string, int>> (str, PrefixStyle.Base128,2);
				foreach (var req in assemblyloadIds.ToArray()) {
					if(false == loadedBins.ContainsKey(req.Key))
						Load (req.Key);
				}
				// before loading this assembly, make sure the others are loaded.
				Assembly asm = null;
				if(bytedata.Length > 0){
					var rasm = Assembly.Load (bytedata);
					var guid = Guid.Parse (rasm.GetCustomAttribute<GuidAttribute> ().Value);
					var asms = AppDomain.CurrentDomain.GetAssemblies ();
					asm = asms.FirstOrDefault (a => a.FullName == rasm.FullName);
					if (loadedAssemblies.Contains (guid))
						return;
					asm = asm ?? Assembly.Load (bytedata);
				}


				loadedBins [filepath] = import ? 0 : assemblyStoreId++;

				Dict<int,int> translation = new Dict<int, int> ();
				foreach (var item in assemblyloadIds) {
					translation [item.Value] = loadedBins [item.Key];
				}
				translation [0] = loadedBins [filepath];
				Uid.LoadAssemblyTranslation = translation;

				int fieldIndex = 3;

				while (str.CanRead) {
					var s = ProtoBuf.Serializer.DeserializeWithLengthPrefix<string> (str, PrefixStyle.Base128,fieldIndex++);
					if (s == null)
						break;
					var s2 = ProtoBuf.Serializer.DeserializeWithLengthPrefix<string> (str, PrefixStyle.Base128,fieldIndex++);
					var tp = System.Type.GetType (s);
					var tp2 = System.Type.GetType (s2);
					object obj;
					bool ok = ProtoBuf.Serializer.NonGeneric.TryDeserializeWithLengthPrefix(str, PrefixStyle.Base128, fld => tp2, out obj);
					fieldIndex++;
					Assert.IsTrue (ok);
					GetModule (tp).LoadData (asm, obj);
				}
				Uid.LoadAssemblyTranslation = new Dict<int,int>();
			}
		}

		public void LoadReference(string path){
			Load (path);
		}

		public void AddFunctionInvocation(Type objType, string FunctionName, string alias = null){
			alias = alias ?? FunctionName;
			var method = objType.GetMethod (FunctionName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.IsTrue (method != null);
			FunctionInvocation [Sym (FunctionName)] = method;
			this.FunctionName [Sym (FunctionName)] = alias;
			var ret = method.ReturnType;
			var pret = getPhotonType (ret);
			this.FunctionReturnType.Add (Sym (FunctionName), pret);
			var args = method.GetParameters ().Select (x => this.DefineArgument (x.Name, getPhotonType (x.ParameterType)));
			var arg_types = args.Select (x => ArgumentType.Get(x)).ToArray ();
			this.FunctionArguments.Add (Sym (FunctionName), args);
		}

	}
}