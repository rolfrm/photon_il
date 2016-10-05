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
        public static Uid CallExpression = Uid.CreateNew();
        public static Uid MacroExpression = Uid.CreateNew();
        public Dict<Uid, Types> types = new Dict<Uid, Types>();
        public Dict<Uid, string> type_name = new Dict<Uid, string>();
        public Dict<Uid, int> type_size = new Dict<Uid, int>();
        public Dict<Uid, bool> is_floating_point = new Dict<Uid, bool>();

        public Dict<Uid, Uid> FunctionReturnType = new Dict<Uid, Uid>();
        public MultiDict<Uid, Uid> FunctionArgTypes = new MultiDict<Uid, Uid>();
        public Dict<Uid, MethodInfo> FunctionInvocation = new Dict<Uid, MethodInfo>();
        public Dict<Uid, string> FunctionName = new Dict<Uid, string>();
        public Dict<Uid, Uid> FunctionType = new Dict<Uid, Uid>();

        public Dict<Uid, Generator> IlGenerators = new Dict<Uid, Generator>();
        public Dict<Uid, Func<Uid, ILGenerator, IlGen, Uid>> Macros = new Dict<Uid, Func<Uid, ILGenerator, IlGen, Uid>>();

        public delegate Uid Generator(Uid exprid, ILGenerator gen);

        public Uid AddPrimitive(string name, int size, bool is_float = false)
        {
            var prim = Uid.CreateNew();
            types.Add(prim, Types.Primitive);
            type_size.Add(prim, size);
            type_name.Add(prim, name);
            if (is_float)
                is_floating_point.Add(prim, is_float);
            return prim;
        }

        public Uid DefineFunctionType(string name = null)
        {
            var ftype = Uid.CreateNew();
            types.Add(ftype, Types.Function);

            return ftype;
        }

        public void AddFunctionArg(Uid fcn, Uid argType)
        {
            FunctionArgTypes.Add(fcn, argType);
        }

        public void SetFunctionReturnType(Uid fcn, Uid returnType)
        {
            FunctionReturnType[fcn] = returnType;
        }

        public Uid DefineFunction(string name, Uid type) {
            var Id = Uid.CreateNew();
            FunctionName.Add(Id, name);
            FunctionType.Add(Id, type);
            return Id;
        }

        public IlGen()
        {
            U8Type = AddPrimitive("u8", 1);
            U16Type = AddPrimitive("u16", 2);
            U32Type = AddPrimitive("u32", 4);
            I32Type = AddPrimitive("i32", 4);
            F32Type = AddPrimitive("f32", 4, is_float: true);
            VoidType = AddPrimitive("void", 0);
            StringType = Uid.CreateNew();
            IlGenerators.Add(CallExpression, GenCall);
            Debug.Assert(this.type_size.Get(U8Type) == 1);
        }

        public readonly Uid U8Type;
        public readonly Uid U16Type;
        public readonly Uid U32Type;
        public readonly Uid I32Type;
        public readonly Uid F32Type;
        public readonly Uid VoidType;
        public readonly Uid StringType;

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

        bool IsExpression(Uid id)
        {
            return true;
        }

        public Uid GenSubCall(Uid expr, ILGenerator il)
        {
            var variable = variableType.Get(expr);
            if (variable != Uid.Default) {
                if (this.types.Get(variable) == Types.Primitive) {
                    int size = this.type_size.Get(variable);
                    bool isfloat = this.is_floating_point.Get(variable);
                    if (isfloat && size == 4)
                        il.Emit(OpCodes.Ldc_R4, (float)Convert.ChangeType(this.variableValue.Get(expr), typeof(float)));
                    else if (isfloat && size == 8)
                        il.Emit(OpCodes.Ldc_R8, (double)Convert.ChangeType(this.variableValue.Get(expr), typeof(double)));
                    if (size <= 4)
                        il.Emit(OpCodes.Ldc_I4, (int)Convert.ChangeType(this.variableValue.Get(expr), typeof(int)));
                    else if (size == 8)
                        il.Emit(OpCodes.Ldc_I8, (long)Convert.ChangeType(this.variableValue.Get(expr), typeof(long)));
                    else
                        throw new Exception("Cannot load type");
                    return variable;
                }
            }
            if (localSymbols.Value.ContainsKey(expr))
            {
                var local = localSymbols.Value.Get(expr);
                il.Emit(OpCodes.Ldloc, local.Local);
                return local.TypeId;
            }
            var subexprs = SubExpressions.Get(expr);
            if (subexprs.Length == 0)
                throw new Exception("Invalid expression");
            var gen = Macros.Get(subexprs[0]);
            if (gen != null)
                return (Uid)gen(expr, il, this);
            return GenCall(expr, il);

        }

        public Type getNetType(Uid typeid) {
            if (this.types.Get(typeid) == Types.Primitive) {
                int size = this.type_size.Get(typeid);
                bool isfloat = this.is_floating_point.Get(typeid);
                if (isfloat && size == 4)
                    return typeof(float);
                else if (isfloat && size == 8)
                    return typeof(double);
                else if (size == 4)
                    return typeof(int);
                else if (size == 8)
                    return typeof(long);
                else if (size == 2)
                    return typeof(short);
                else if (size == 1)
                    return typeof(byte);
                else
                    throw new Exception("Cannot load type");
            } else if (types.Get(typeid) == Types.Struct) {
                return getStructType(typeid);
            }

            throw new Exception("Unsupported type");
        }

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
                    typebuilder.DefineField(argumentName.Get(member), getNetType(argumentType.Get(member)),
                        FieldAttributes.Public);
                generatedStructs.Add(typeid, typebuilder.CreateType());
            }
            return generatedStructs.Get(typeid);
        }

        public Dict<Uid, Type> generatedStructs = new Dict<Uid, Type>();

        public StackLocal<Uid> ExpectedType = new StackLocal<Uid>();
        public Uid GenCall(Uid expr, ILGenerator il)
        {
            var subexprs = SubExpressions.Get(expr);
            var function = subexprs[0];

            var mt = FunctionType.Get(function);
            var args = FunctionArgTypes.Get(mt);
            var returnType = FunctionReturnType.Get(mt);

            if (args.Length != subexprs.Length - 1)
                throw new Exception("Unsupported number of arguments");

            LocalBuilder[] stlocs = new LocalBuilder[subexprs.Length - 1];
            for (int i = 1; i < subexprs.Length; i++) {
                using (var item = ExpectedType.WithValue(args[i - 1])) {

                    Uid type = GenSubCall(subexprs[i], il);
                    if (type != args[i - 1])
                        throw new CompilerError(subexprs[i], "Invalid type of arg {0}. Expected {1}, got {2}.", i - 1, args[i - 1], type);
                    stlocs[i - 1] = il.DeclareLocal(getNetType(type));
                    il.Emit(OpCodes.Stloc, stlocs[i - 1]);
                }
            }

            var m = FunctionInvocation.Get(function);

            foreach (var loc in stlocs)
                il.Emit(OpCodes.Ldloc, loc);
            il.Emit(OpCodes.Call, m);

            return returnType;
        }

        ModuleBuilder newModule()
        {
            AssemblyBuilder a = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("myasm"),
                AssemblyBuilderAccess.RunAndSave);
            return a.DefineDynamicModule("myasm.dll");
        }

        public MethodInfo GenerateIL(Uid expr)
        {

            AssemblyBuilder a = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("myasm"),
                AssemblyBuilderAccess.RunAndSave);
            var module = a.DefineDynamicModule("myasm");
            var tb = module.DefineType("mytype", TypeAttributes.Class | TypeAttributes.Public);

            var name = FunctionName.Get(expr) ?? "_";
            var body = functionBody.Get(expr);
            var ftype = FunctionType.Get(expr);
            if (body != Uid.Default)
            {

                var fn = tb.DefineMethod(name, MethodAttributes.Static | MethodAttributes.Public,
                    getNetType(FunctionReturnType.Get(ftype)),
                    FunctionArgTypes.Get(ftype).Select(getNetType).ToArray());
                var ilgen = fn.GetILGenerator();

                using (localSymbols.WithValue(new Dict<Uid, LocalSymData>()))
                {
                    GenSubCall(body, ilgen);
                }
                ilgen.Emit(OpCodes.Ret);
                Type t = tb.CreateType();
                var runm = t.GetMethod(name);
                //Action x;
                module.CreateGlobalFunctions();
                //a.Save("myasm.dll");
                //var assembly = Assembly.LoadFrom ("myasm.dll");
                //var extypes = assembly.GetExportedTypes ();
                return runm;
            }
            else
            {

                var fn = tb.DefineMethod("run", MethodAttributes.Static | MethodAttributes.Public);

                var ilgen = fn.GetILGenerator();
                using (localSymbols.WithValue(new Dict<Uid, LocalSymData>()))
                {
                    GenSubCall(expr, ilgen);
                }
                ilgen.Emit(OpCodes.Ret);
                Type t = tb.CreateType();
                var runm = t.GetMethod("run");
                //Action x;
                module.CreateGlobalFunctions();
                //a.Save("myasm.dll");
                //var assembly = Assembly.LoadFrom ("myasm.dll");
                //var extypes = assembly.GetExportedTypes ();
                return runm;
                //runm.Invoke (null, null);
            }
        }
        public delegate Uid SubExpressionDelegate(params Uid[] uids);

        public SubExpressionDelegate Sub { get { return Expression; } }

        public Uid Expression(params Uid[] uids)
        {
            return CreateExpression(uids);
        }

        Dict<Uid, string> argumentName = new Dict<Uid, string>();
        Dict<Uid, Uid> argumentType = new Dict<Uid, Uid>();

        public Uid DefineArgument(string name = null, Uid type = default(Uid)) {
            var id = Uid.CreateNew();
            if (name != null)
                argumentName.Add(id, name);
            if (type != default(Uid))
                argumentType.Add(id, type);
            return id;
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

        public Uid GetStructAccessor(Uid member) {
            return Uid.CreateNew();
        }

        Uid InitStruct;
        public static Uid GenStruct(Uid expr, ILGenerator il, IlGen gen)
        {
            var subexprs = gen.SubExpressions.Get(expr);
            gen.GenStructConstructorIl(il, subexprs[1]);
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

        void GenStructConstructorIl(ILGenerator il, Uid structid) {
            il.Emit(OpCodes.Ldloc, il.DeclareLocal(getNetType(structid)));
        }

        void GenStructAccessorIl(ILGenerator il, Uid structid, Uid member) {
            //il.Emit(OpCodes.Ld
        }

        FieldInfo getNetFieldInfo(Uid member, Uid structType)
        {
            throw new NotImplementedException();
        }

        Uid GenStructAccess(ILGenerator il, Uid[] exprs) {
            Uid member = exprs[0];
            Uid structExpr = exprs[0];
            Uid structType = GenCall(structExpr, il);
            il.Emit(OpCodes.Ldfld, getNetFieldInfo(member, structType));
            return argumentType.Get(member);
        }

        public Uid GetFunctionType(Uid returnType, params Uid[] argTypes) {
            var existing = FunctionReturnType.Where(x => x.Value == returnType)
                .FirstOrDefault(x => Enumerable.SequenceEqual(argTypes, FunctionArgTypes.Get(x.Key))).Key;
            if (existing != Uid.Default)
                return existing;
            var @new = Uid.CreateNew();
            if (returnType != VoidType)
                FunctionReturnType.Add(@new, returnType);
            FunctionArgTypes.Add(@new, argTypes);
            return @new;
        }

        MultiDict<Uid, Uid> functionArguments = new MultiDict<Uid, Uid>();
        public Uid DefineFunction(string name, Uid fcnType, params Uid[] arguments) {

            var id = Uid.CreateNew();
            variableValue.Add(id, name);
            FunctionType.Add(id, fcnType);
            functionArguments.Add(id, arguments);
            return id;
        }

        Dict<Uid, Uid> functionBody = new Dict<Uid, Uid>();

        public Uid DefineFcnBody(Uid fcn) {
            var fcnBody = Uid.CreateNew();
            functionBody.Add(fcn, fcnBody);
            return fcnBody;
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

        public Uid genProgn(Uid expr, ILGenerator il, IlGen gen)
        {
            var exprs = gen.SubExpressions.Get(expr);
            if (exprs.Length == 1)
                return VoidType;
            if(exprs.Length == 2)
            {
                return GenSubCall(exprs[1], il);
            }
            for (int i = 1; i < exprs.Length; i++)
            {
                Uid type = GenSubCall(exprs[i], il);
                if (i == exprs.Length - 1)
                {
                    return type;
                }
                else if(type != VoidType)
                {
                    il.Emit(OpCodes.Pop);
                }
            }
            Debug.Fail("Unreachable");
            return Uid.Default;
        }

        public readonly Uid Add = Uid.CreateNew();
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

        struct LocalSymData
        {
            public LocalBuilder Local;
            public Uid TypeId;
        }

        StackLocal<Dict<Uid, LocalSymData>> localSymbols = new StackLocal<Dict<Uid, LocalSymData>>(new Dict<Uid, LocalSymData>());

        public Uid genLet(Uid expr, ILGenerator il, IlGen gen)
        {
            var exprs = gen.SubExpressions.Get(expr);
            Debug.Assert(Symbols.Contains(exprs[1]));
            Uid type = GenSubCall(exprs[2], il);
            var local = il.DeclareLocal(getNetType(type));
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Stloc, local);
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
	}

}