using System.Reflection.Emit;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PhotonIl
{
	public class CompilerError : Exception
	{
		public readonly Uid Expr;
		public CompilerError(Uid expr, string msg, params object[] args) :base(string.Format (msg, args)){
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

    public class IlGen
    {
		
		public Functions F;

        public static Uid CallExpression = Uid.CreateNew();
        public static Uid MacroExpression = Uid.CreateNew();
        public Dict<Uid, Types> types = new Dict<Uid, Types>();
        public Dict<Uid, string> type_name = new Dict<Uid, string>();
        public Dict<Uid, int> type_size = new Dict<Uid, int>();
        public Dict<Uid, bool> is_floating_point = new Dict<Uid, bool>();

        public Dict<Uid, Uid> FunctionReturnType = new Dict<Uid, Uid>();
        
        public Dict<Uid, MethodInfo> FunctionInvocation = new Dict<Uid, MethodInfo>();
        public Dict<Uid, string> FunctionName = new Dict<Uid, string>();

        public Dict<Uid, Generator> IlGenerators = new Dict<Uid, Generator>();
        public Dict<Uid, Func<Uid, IlGen, Uid>> Macros = new Dict<Uid, Func<Uid, IlGen, Uid>>();

        public delegate Uid Generator(Uid exprid);

		public Uid AddPrimitive(string name, int size, Type dotNetType = null, bool is_float = false)
        {
            var prim = Uid.CreateNew();
            types.Add(prim, Types.Primitive);
            type_size.Add(prim, size);
            type_name.Add(prim, name);
            if (is_float)
                is_floating_point.Add(prim, is_float);
			generatedStructs.Add (prim, dotNetType);
            return prim;
        }

        public Uid DefineFunctionType(string name = null)
        {
            var ftype = Uid.CreateNew();
            types.Add(ftype, Types.Function);

            return ftype;
        }

        public void AddFunctionArg(Uid fcn, Uid arg)
        {
			FunctionArguments.Add (fcn, arg);
        }

        public void SetFunctionReturnType(Uid fcn, Uid returnType)
        {
            FunctionReturnType[fcn] = returnType;
        }

		Uid addCSharpType(Type type){
			if (type.IsValueType == false)
				throw new Exception ("Unable to add non-value type");

			Uid uid = Uid.CreateNew ();

			this.types.Add (uid, Types.Struct);
			this.generatedStructs.Add (uid, type);
			this.type_name.Add (uid, type.Name);
			return uid;
		}

        public IlGen()
        {
			
			U8Type = AddPrimitive("u8", 1, typeof(byte));
			U16Type = AddPrimitive("u16", 2, typeof(short));
			U32Type = AddPrimitive("u32", 4, typeof(uint));
			I32Type = AddPrimitive("i32", 4, typeof(int));
			F32Type = AddPrimitive("f32", 4, typeof(float),is_float: true);
			F64Type = AddPrimitive("f64", 8, typeof(double),is_float: true);

            VoidType = AddPrimitive("void", 0);
			StringType = AddPrimitive ("string", 0, typeof(string));
			UidType = addCSharpType (typeof(Uid));

            IlGenerators.Add(CallExpression, GenCall);
            Debug.Assert(this.type_size.Get(U8Type) == 1);
			F = new Functions (this);

			Add.ToString ();Subtract.ToString ();Divide.ToString ();Multiply.ToString ();

        }

		public Uid getPhotonType(Type t){
			var entry = generatedStructs.FirstOrDefault (x => x.Value == t);
			if (entry.Key != Uid.Default)
				return entry.Key;
			throw new Exception ("Type not found: " + t.Name);
		}

        public readonly Uid U8Type;
        public readonly Uid U16Type;
        public readonly Uid U32Type;
        public readonly Uid I32Type;
        public readonly Uid F32Type;
		public readonly Uid F64Type;
        public readonly Uid VoidType;
        public readonly Uid StringType;

		public readonly Uid UidType;

        public HashSet<Uid> Expressions = new HashSet<Uid>();
        public MultiDict<Uid, Uid> SubExpressions = new MultiDict<Uid, Uid>();

        public Uid CreateExpression(Uid baseexpr)
        {
            var uid = Uid.CreateNew();
            Expressions.Add(uid);
            AddSubExpression(uid, baseexpr);
            return uid;
        }

        public Uid CreateExpression(Uid[] items) {
            var uid = Uid.CreateNew();
            Expressions.Add(uid);
            AddSubExpression(uid, items);
            return uid;
        }

        public void AddSubExpression(Uid expression, params Uid[] subexpression)
        {
            foreach (var s in subexpression)
                SubExpressions.Add(expression, s);
        }

        public Dict<Uid, string> variableName = new Dict<Uid, string>();
        public Dict<Uid, object> variableValue = new Dict<Uid, object>();
        public Dict<Uid, Uid> variableType = new Dict<Uid, Uid>();

        public Uid DefineVariable(Uid type, string name = null, object value = null)
        {
            Uid varid = Uid.CreateNew();
            if (name != null)
                variableName.Add(varid, name);
            if (value != null)
                variableValue.Add(varid, value);
            variableType.Add(varid, type);

            return varid;
        }

        public readonly Dict<Uid, object> ConstantValue = new Dict<Uid, object>();
        public readonly Dict<Uid, Uid> ConstantType = new Dict<Uid, Uid>();
        public Uid DefineConstant(Uid type, object value, string name = null)
        {
            var s = Sym(name);
            ConstantType.Add(s, type);
            ConstantValue.Add(s, value);
            return s;
        }


        bool IsExpression(Uid id)
        {
            return true;
        }

		Dict<Uid,FieldInfo> variableItems = new Dict<Uid, FieldInfo>();

		FieldInfo getVariable(Uid variableId)
		{
			FieldInfo v = variableItems.Get (variableId);
			if (v == null) {
				var typeid = variableType.Get (variableId);
				if (typeid == Uid.Default)
					return null;
				
				var name = variableName.Get (variableId) ?? "AnonVariable";
				var val = variableValue.Get (variableId);
				var mod = newModule ();
				var tp = mod.DefineType ("no-name");

				tp.DefineField (name, GetCSType (typeid), FieldAttributes.Public | FieldAttributes.Static); 
				var objtype = tp.CreateType ();
				v = objtype.GetField (name, BindingFlags.Public | BindingFlags.Static);

				if(val != null)
					v.SetValue(null, val);
				variableItems.Add (variableId, v);
			}
			return v;
		}

        public Uid GenSubCall(Uid expr)
        {
            {
                var constantType = ConstantType.Get(expr);
                if (constantType != Uid.Default)
                {
                    if (this.types.Get(constantType) == Types.Primitive)
                    {
                        int size = this.type_size.Get(constantType);
                        bool isfloat = this.is_floating_point.Get(constantType);
                        if (isfloat && size == 4)
							Interact.Emit(OpCodes.Ldc_R4, (float)Convert.ChangeType(ConstantValue.Get(expr), typeof(float)));
                        else if (isfloat && size == 8)
							Interact.Emit(OpCodes.Ldc_R8, (double)Convert.ChangeType(ConstantValue.Get(expr), typeof(double)));
                        else if (size <= 4)
							Interact.Emit(OpCodes.Ldc_I4, (int)Convert.ChangeType(ConstantValue.Get(expr), typeof(int)));
                        else if (size == 8)
							Interact.Emit(OpCodes.Ldc_I8, (long)Convert.ChangeType(ConstantValue.Get(expr), typeof(long)));
                        else
                            throw new Exception("Cannot load type");
                        return constantType;
                    }
                }
            }

            {
				var variableMember = getVariable (expr);
				if (variableMember != null) {
					Interact.Emit (OpCodes.Ldnull);
					Interact.Emit (OpCodes.Ldfld, variableMember);
					return variableType.Get (expr);
				}
            }
            
            if (localSymbols.Value.ContainsKey(expr))
            {
                var local = localSymbols.Value.Get(expr);
                if (ReferenceReq.Contains(expr))
                {
					if (local.Local != default(LocalBuilder))
						Interact.Emit (OpCodes.Ldloca_S, local.Local);
					else
						Interact.Emit (OpCodes.Ldarga, local.ArgIndex);
                    ReferenceReq.Remove(expr);
                }
                else
                {
					if (local.Local != default(LocalBuilder))
						Interact.Emit (OpCodes.Ldloc, local.Local);
					else
						Interact.Emit (OpCodes.Ldarg, local.ArgIndex);
                }
                return local.TypeId;
            }
            var subexprs = SubExpressions.Get(expr);
			if (subexprs.Count == 0)
                throw new Exception("Invalid expression");
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
						return GenSubCall (ret);
				}
			}


            return GenCall(expr);

        }

        public Type GetCSType(Uid typeid) {
			if (typeid == VoidType)
				return typeof(void);
            if (generatedStructs.Get (typeid) != null) {
				return generatedStructs.Get (typeid);
            } else if (types.Get(typeid) == Types.Struct) {
                return getStructType(typeid);
            }
			foreach (var f in TypeGetters) {
				Type t = f (typeid);
				if (t != null)
					return t;
			}


            throw new Exception("Unsupported type");
        }

		public delegate Type TypeGetter (Uid expr);

		public readonly List<TypeGetter> TypeGetters = new List<TypeGetter>();

        Type getStructType(Uid typeid)
        {
            if (generatedStructs.ContainsKey(typeid) == false)
            {
                var structNameVar = structName.Get(typeid);
                var name = variableValue.Get(structNameVar) as string;
                var typebuilder = newModule().DefineType(name ?? "uniqueid",
                    TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                    typeof(ValueType));
                var members = structMembers.Get(typeid);
                foreach (var member in members)
                    typebuilder.DefineField(ArgumentName.Get(member), GetCSType(ArgumentType.Get(member)),
                        FieldAttributes.Public);
                generatedStructs.Add(typeid, typebuilder.CreateType());
            }
            return generatedStructs.Get(typeid);
        }

        public Dict<Uid, Type> generatedStructs = new Dict<Uid, Type>();

        public StackLocal<Uid> ExpectedType = new StackLocal<Uid>();
        public Uid GenCall(Uid expr)
        {
            
            var subexprs = SubExpressions.Get(expr);
            var function = subexprs[0];
            if (FunctionInvocation.Get(function) == null)
            {
                GenerateIL(function);
            }
            var mt = function;
			var args = FunctionArguments.Get(function);
            var returnType = FunctionReturnType.Get(mt);

			if (args.Count != subexprs.Count - 1)
                throw new Exception("Unsupported number of arguments");

			LocalBuilder[] stlocs = new LocalBuilder[subexprs.Count - 1];
			for (int i = 1; i < subexprs.Count; i++) {
                using (var item = ExpectedType.WithValue(args[i - 1])) {

                    Uid type = GenSubCall(subexprs[i]);
					if (type != ArgumentType.Get(args[i - 1]))
                        throw new CompilerError(subexprs[i], "Invalid type of arg {0}. Expected {1}, got {2}.", i - 1, args[i - 1], type);
					stlocs[i - 1] = Interact.DeclareLocal(GetCSType(type));
					Interact.Emit(OpCodes.Stloc, stlocs[i - 1]);
                }
            }

            var m = FunctionInvocation.Get(function);

            foreach (var loc in stlocs)
				Interact.Emit(OpCodes.Ldloc, loc);
			Interact.Emit(OpCodes.Call, m);

            return returnType;
        }

        ModuleBuilder newModule()
        {
            AssemblyBuilder a = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("myasm"),
                AssemblyBuilderAccess.RunAndSave);
            return a.DefineDynamicModule("myasm.dll", "DynAsm2.mod");
        }

		public Uid GenExpression (Uid expr, params Uid[] arguments)
		{
			Interact.Load (this, null);
			Uid rt;
			using (localSymbols.WithValue (new Dict<Uid, LocalSymData> ())) {
					
				short paramIndex = 0;
				foreach (var arg in arguments) {
					var argname = ArgumentName.Get (arg) ?? ("arg_" + arg);
					localSymbols.Value.Add (arg, new LocalSymData{ ArgIndex = paramIndex, TypeId = ArgumentType.Get (arg) });
					paramIndex += 1;
				}
				rt = GenSubCall (expr);
			}
			return rt;
		}

		public MethodInfo GenerateIL (Uid expr)
		{
			AssemblyBuilder asmBuild = AppDomain.CurrentDomain.DefineDynamicAssembly (
				                                    new AssemblyName ("DynAsm"), AssemblyBuilderAccess.RunAndSave, System.IO.Directory.GetCurrentDirectory ());
			var module = asmBuild.DefineDynamicModule ("DynMod");
			var tb = module.DefineType ("MyType", TypeAttributes.Class | TypeAttributes.Public);

			var name = FunctionName.Get (expr) ?? "_";
			var body = functionBody.Get (expr);
			var ftype = expr;
			var rtype = FunctionReturnType.Get (ftype);
			MethodBuilder fn = tb.DefineMethod (name, MethodAttributes.Static | MethodAttributes.Public,
				                                GetCSType (rtype),
				                                FunctionArguments.Get (ftype).Select (arg => GetCSType (ArgumentType.Get (arg))).ToArray ());
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
				rt = GenSubCall (body);
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

		public MethodInfo GenerateILOld (Uid expr)
		{
			AssemblyBuilder asmBuild = AppDomain.CurrentDomain.DefineDynamicAssembly (
				                           new AssemblyName ("DynAsm"), AssemblyBuilderAccess.RunAndSave, System.IO.Directory.GetCurrentDirectory ());
			var module = asmBuild.DefineDynamicModule ("DynMod");
			var tb = module.DefineType ("MyType", TypeAttributes.Class | TypeAttributes.Public);

			var name = FunctionName.Get (expr) ?? "_";
			var body = functionBody.Get (expr);
			var ftype = expr;

			var fn = tb.DefineMethod ("run", MethodAttributes.Static | MethodAttributes.Public);

			var ilgen = fn.GetILGenerator ();
			using (localSymbols.WithValue (new Dict<Uid, LocalSymData> ()))
				GenSubCall (expr);
			ilgen.Emit (OpCodes.Ret);
			Type t = tb.CreateType ();
			var runm = t.GetMethod ("run");
			module.CreateGlobalFunctions ();
			return runm;

		}

        public delegate Uid SubExpressionDelegate(params Uid[] uids);

        public SubExpressionDelegate Sub { get { return Expression; } }

        public Uid Expression(params Uid[] uids)
        {
            return CreateExpression(uids);
        }

        public Dict<Uid, string> ArgumentName = new Dict<Uid, string>();
        public Dict<Uid, Uid> ArgumentType = new Dict<Uid, Uid>();

        public Uid DefineArgument(string name = null, Uid type = default(Uid)) {
            var id = Uid.CreateNew();
            if (name != null)
                ArgumentName.Add(id, name);
            if (type != default(Uid))
                ArgumentType.Add(id, type);
            return id;
        }

		public Uid Arg(string name, Uid type){
			return DefineArgument (name, type);
		}

        // Variable that defines the struct name.
        Dict<Uid, Uid> structName = new Dict<Uid, Uid>();
        MultiDict<Uid, Uid> structMembers = new MultiDict<Uid, Uid>();
        public Uid DefineStruct(Uid nameVar = default(Uid), params Uid[] arguments)
        {
            var id = Uid.CreateNew();

            if (nameVar != default(Uid))
                structName.Add(id, nameVar);
            structMembers.Add(id, arguments);
            types.Add(id, Types.Struct);
            return id;
        }

        Uid getStructAccess;
        public Uid GetStructAccessor(Uid member, Uid structexpr) {
            if (getStructAccess == Uid.Default)
            {
                getStructAccess = Uid.CreateNew();
                Macros.Add(getStructAccess, genStructAccess);
            }
            var structid = structMembers.Entries.FirstOrDefault(e => e.Value.Contains(member)).Key;
            return Sub(getStructAccess, structid, member, structexpr);
        }

        HashSet<Uid> ReferenceReq = new HashSet<Uid>();

        public static Uid genStructAccess(Uid expr, IlGen gen)
        {
            var sexprs = gen.SubExpressions.Get(expr);
            var structid = sexprs[1];
            var memberid = sexprs[2];
            FieldInfo field = gen.getNetFieldInfo(memberid, structid);
            Uid valueExpr = gen.SetExprs.Get(expr);
            
            if (valueExpr != Uid.Default)
            {
                gen.SetExprs.Remove(expr);
                gen.ReferenceReq.Add(sexprs[3]);
                gen.GenSubCall(sexprs[3]);
                if (gen.ReferenceReq.Contains(sexprs[3]))
                    throw new Exception("Unable to access right valaue");
				Interact.Emit(OpCodes.Dup);
                gen.GenSubCall(valueExpr);
				Interact.Emit(OpCodes.Stfld, field);
				Interact.Emit(OpCodes.Ldfld, field);
                
            }
            else
            {
                gen.GenSubCall(sexprs[3]);
				Interact.Emit(OpCodes.Ldfld, field);
            }

            return gen.ArgumentType.Get(memberid);
        }

        Uid InitStruct;
        public static Uid GenStruct(Uid expr, IlGen gen)
        {
            var subexprs = gen.SubExpressions.Get(expr);
            gen.GenStructConstructorIl(subexprs[1]);
            return subexprs[1];
        }
        public Uid[] GetStructConstructor(Uid struct_id) {
            if (InitStruct == Uid.Default)
            {
                InitStruct = Uid.CreateNew();
                Macros.Add(InitStruct, GenStruct);
            }

            return new[] { InitStruct, struct_id };
        }

        void GenStructConstructorIl(Uid structid) {
			Interact.Emit(OpCodes.Ldloc, Interact.DeclareLocal(GetCSType(structid)));
        }

        FieldInfo getNetFieldInfo(Uid member, Uid structType)
        {
            var args = structMembers.Get(structType);
			var idx = args.IndexOf(member);
            return GetCSType(structType).GetFields()[idx];
        }


        MultiDict<Uid, Uid> FunctionArguments = new MultiDict<Uid, Uid>();
        public Uid DefineFunction(string name, Uid returnType, params Uid[] arguments) {

            var id = Uid.CreateNew();
			FunctionReturnType.Add(id, returnType);
            FunctionArguments.Add(id, arguments);
			FunctionName.Add (id, name);
            return id;
        }

        Dict<Uid, Uid> functionBody = new Dict<Uid, Uid>();

		public void DefineFcnBody(Uid fcn, Uid body) 
		{
            functionBody.Add(fcn, body);
        }

        Uid _progn;
        public Uid Progn
        {
            get
            {
                if(_progn == Uid.Default)
                {
                    _progn = Uid.CreateNew();
                    Macros.Add(_progn, genProgn);
                }
                return _progn;
            }
        }

        public Uid genProgn(Uid expr, IlGen gen)
        {
            var exprs = gen.SubExpressions.Get(expr);
			if (exprs.Count == 1)
                return VoidType;
			if(exprs.Count == 2)
            {
                return GenSubCall(exprs[1]);
            }
			for (int i = 1; i < exprs.Count; i++)
            {
                Uid type = GenSubCall(exprs[i]);
				if (i == exprs.Count - 1)
                {
                    return type;
                }
                else if(type != VoidType)
                {
					Interact.Emit(OpCodes.Pop);
                }
            }
            Debug.Fail("Unreachable");
            return Uid.Default;
        }

        public Uid Add
        {
			get { return genBaseFunctor (OpCodes.Add, "+"); }
        }

		public Uid Subtract
		{
			get { return genBaseFunctor (OpCodes.Sub, "-"); }
		}

		public Uid Multiply
		{
			get { return genBaseFunctor (OpCodes.Mul, "*"); }
		}

		public Uid Divide
		{
			get { return genBaseFunctor (OpCodes.Div, "/"); }
		}

		public Uid genAdd(OpCode opcode, Uid expr, IlGen gen )
		{
			var subs = gen.SubExpressions.Get(expr);
			if (subs.Count < 2)
				throw new Exception("Invalid number of arguments for +");
			Uid type = GenSubCall(subs[1]);
			for(int i = 2; i < subs.Count; i++)
			{
				Uid t2 = GenSubCall(subs[i]);
				if (type != t2)
					throw new Exception("Invalid type for +");
				Interact.Emit(opcode);
			}
			return type;
		}

		Dict<OpCode, Uid> BaseOpCodes = new Dict<OpCode, Uid>();
		public readonly Dict<Uid, string> MacroNames = new Dict<Uid, string>();
		public Uid genBaseFunctor(OpCode c, string name)
		{
			if (BaseOpCodes.ContainsKey (c) == false) {
				Uid id = Uid.CreateNew ();
				BaseOpCodes.Add (c, id);
				Macros.Add(id, (x, z) => genAdd (c, x, z));
				MacroNames.Add (id, name);

			}
			return BaseOpCodes.Get (c);
		}


        Uid _let;
        public Uid Let {
            get
            {
                if (_let == Uid.Default)
                {
                    _let = Uid.CreateNew();
                    Macros.Add(_let, genLet);
                }
                return _let;
            }
        }

        Uid _set;
        public Uid Set
        {
            get
            {
                if(_set == Uid.Default)
                {
                    _set = Uid.CreateNew();
                    Macros.Add(_set, genSet);
                }
                return _set;
            }
        }

        //(setf (member-x sym) 5)
        // vs
        //(set-member-x sym 5)
        // Setf needs to communicate with whatever the inner form is for that
        // to work. 
        public Dict<Uid,Uid> SetExprs = new Dict<Uid,Uid>();
        public Uid genSet(Uid expr, IlGen gen)
        {
            var exprs = SubExpressions.Get(expr);
            // set, accessor, value
            SetExprs.Add(exprs[1],exprs[2]);
            Uid typeid2 = GenSubCall(exprs[1]);
            if (SetExprs.ContainsKey(exprs[1]))
                throw new Exception("Sub expression does not support set");
            return typeid2;
        }

        struct LocalSymData
        {
            public LocalBuilder Local;
			public short ArgIndex;
            public Uid TypeId;
        }

        StackLocal<Dict<Uid, LocalSymData>> localSymbols = new StackLocal<Dict<Uid, LocalSymData>>(new Dict<Uid, LocalSymData>());

        public Uid genLet(Uid expr, IlGen gen)
        {
            var exprs = gen.SubExpressions.Get(expr);
            Debug.Assert(Symbols.Contains(exprs[1]));
            Uid type = GenSubCall(exprs[2]);
			var local = Interact.DeclareLocal(GetCSType(type));
			Interact.Emit(OpCodes.Dup);
			Interact.Emit(OpCodes.Stloc, local);
            localSymbols.Value.Add(exprs[1], new LocalSymData { Local = local, TypeId = type });
            return type;
        }

        Dict<string, Uid> SymbolNames = new Dict<string, Uid>();
        HashSet<Uid> Symbols = new HashSet<Uid>();
        public Uid Sym(string name = null)
        {
            if (name != null && SymbolNames.ContainsKey(name))
            {
                return SymbolNames[name];
            }
            var @new = Uid.CreateNew();
            if(name != null)
            {
                SymbolNames[name] = @new;
            }
            Symbols.Add(@new);
            return @new;
        }
		Dict<Uid,MacroDelegate> userMacros = new Dict<Uid, MacroDelegate>();
		public delegate Uid MacroDelegate(Uid expr);

		public void AddMacro(Uid id, MacroDelegate d, string macroName = null){
			userMacros.Add (id, d);
			macroName = macroName ?? SymbolNames.FirstOrDefault (x => x.Value == id).Key;
			if (macroName != null)
				MacroNames.Add (id, macroName);
		}

		public void AddMacro(Uid id, MethodInfo m){
			Assert.IsTrue (m.IsStatic && m.IsPublic);
			userMacros.Add (id, expr => (Uid)m.Invoke (null, null));
			MacroNames.Add (id, m.Name);
		}

		public Uid GetFunctionBody(Uid fcn){
			return functionBody.Get (fcn);
		}
	}

}