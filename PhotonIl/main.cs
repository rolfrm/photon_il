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
		Function
	}

	public class IlGen
	{
		public static Uid CallExpression = Uid.CreateNew ();
		public static Uid MacroExpression = Uid.CreateNew ();
		public Dict<Uid, Types> types = new Dict<Uid, Types> ();
		public Dict<Uid, string> type_name = new Dict<Uid, string> ();
		public Dict<Uid, int> type_size = new Dict<Uid, int> ();
		public Dict<Uid, bool> is_floating_point = new Dict<Uid, bool> ();

		public Dict<Uid, Uid> FunctionReturnType = new Dict<Uid, Uid> ();
		public MultiDict<Uid, Uid> FunctionArgTypes = new MultiDict<Uid, Uid> ();
		public Dict<Uid, MethodInfo> FunctionInvocation = new Dict<Uid, MethodInfo>();
		public Dict<Uid, string> FunctionName = new Dict<Uid, string>();
		public Dict<Uid, Uid> FunctionType = new Dict<Uid, Uid>();

		public Dict<Uid, Generator> IlGenerators = new Dict<Uid,Generator>();

		public delegate Uid Generator(Uid exprid, ILGenerator gen);

		public Uid AddPrimitive (string name, int size, bool is_float = false)
		{
			var prim = Uid.CreateNew ();
			types.Add (prim, Types.Primitive);
			type_size.Add (prim, size);
			type_name.Add (prim, name);
			if(is_float)
				is_floating_point.Add (prim, is_float);
			return prim;
		}

		public Uid DefineFunctionType (string name = null)
		{
			var ftype = Uid.CreateNew ();
			types.Add (ftype, Types.Function);

			return ftype;
		}

		public void AddFunctionArg (Uid fcn, Uid argType)
		{
			FunctionArgTypes.Add (fcn, argType);
		}

		public void SetFunctionReturnType (Uid fcn, Uid returnType)
		{
			FunctionReturnType [fcn] = returnType;
		}

		public Uid DefineFunction(string name, Uid type){
			var Id = Uid.CreateNew ();
			FunctionName.Add (Id, name);
			FunctionType.Add (Id, type);
			return Id;
		}

		public IlGen ()
		{
			U8Type = AddPrimitive ("u8", 1);
			U16Type = AddPrimitive ("u16", 2);
			U32Type = AddPrimitive ("u32", 4);
			I32Type = AddPrimitive ("i32", 4);
			F32Type = AddPrimitive ("f32", 4, is_float: true);
			VoidType = AddPrimitive ("void", 0);
			StringType = Uid.CreateNew ();
			IlGenerators.Add (CallExpression, GenCall);
			Debug.Assert (this.type_size.Get (U8Type) == 1);
		}

		public readonly Uid U8Type;
		public readonly Uid U16Type;
		public readonly Uid U32Type;
		public readonly Uid I32Type;
		public readonly Uid F32Type;
		public readonly Uid VoidType;
		public readonly Uid StringType;

		public HashSet<Uid> Expressions = new HashSet<Uid> ();
		public MultiDict<Uid, Uid> SubExpressions = new MultiDict<Uid, Uid> ();

		public Uid CreateExpression (Uid baseexpr)
		{
			var uid = Uid.CreateNew ();
			Expressions.Add (uid);
			AddSubExpression (uid, baseexpr);
			return uid;
		}

		public Uid CreateExpression(Uid[] items){
			var uid = Uid.CreateNew ();
			Expressions.Add (uid);
			AddSubExpression (uid, items);
			return uid;
		}

		public void AddSubExpression (Uid expression, params Uid[] subexpression)
		{
			foreach(var s in subexpression)
				SubExpressions.Add (expression, s);
		}

		public  Dict<Uid, string> variableName = new Dict<Uid, string> ();
		public Dict<Uid, object> variableValue = new Dict<Uid, object> ();
		public Dict<Uid, Uid> variableType = new Dict<Uid, Uid> ();

		public Uid DefineVariable (Uid type, string name = null, object value = null)
		{
			Uid varid = Uid.CreateNew ();
			if (name != null)
				variableName.Add (varid, name);
			if (value != null)
				variableValue.Add (varid, value);
			variableType.Add (varid, type);

			return varid;
		}

		bool IsExpression (Uid id)
		{
			return true;
		}

		public Uid GenSubCall(Uid expr, ILGenerator il)
		{
			var variable = variableType.Get (expr);
			if (variable != Uid.Default) {
				if (this.types.Get (variable) == Types.Primitive) {
					int size = this.type_size.Get (variable);
					bool isfloat = this.is_floating_point.Get (variable);
					if (isfloat && size == 4)
						il.Emit (OpCodes.Ldc_R4, (float)Convert.ChangeType (this.variableValue.Get (expr), typeof(float)));
					else if(isfloat && size == 8)
						il.Emit (OpCodes.Ldc_R8, (double)Convert.ChangeType (this.variableValue.Get (expr), typeof(double)));
					if (size <= 4)
						il.Emit (OpCodes.Ldc_I4, (int)Convert.ChangeType(this.variableValue.Get (expr), typeof(int)));
					else if(size == 8)
						il.Emit (OpCodes.Ldc_I8, (long)Convert.ChangeType(this.variableValue.Get (expr), typeof(long)));
					else
						throw new Exception ("Cannot load type");
					return variable;
				}
			}
			var subexprs = SubExpressions.Get (expr);
			if (subexprs.Length == 0)
				throw new Exception ("Invalid expression");
			var gen = IlGenerators.Get (subexprs [0]);
			return gen (expr, il);
		}

		public Type getNetType(Uid typeid){
			if (this.types.Get (typeid) == Types.Primitive) {
				int size = this.type_size.Get (typeid);
				bool isfloat = this.is_floating_point.Get (typeid);
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
					throw new Exception ("Cannot load type");
			}
			throw new Exception ("Unsupported type");
		}
		public StackLocal<Uid> ExpectedType = new StackLocal<Uid>();
		public Uid GenCall (Uid expr, ILGenerator il)
		{
			var subexprs = SubExpressions.Get (expr);
			if (subexprs.Length <= 1)
				throw new Exception ("Invalid expression");
			var function = subexprs [1];

			var mt = FunctionType.Get (function);
			var args = FunctionArgTypes.Get (mt);
			var returnType = FunctionReturnType.Get (mt);

			if (args.Length != subexprs.Length - 2)
				throw new Exception ("Unsupported number of arguments");
			
			LocalBuilder[] stlocs = new LocalBuilder[subexprs.Length - 2];
			for (int i = 2; i < subexprs.Length; i++) {
				using (var item = ExpectedType.WithValue (args [i - 2])) {
					
					Uid type = GenSubCall (subexprs [i], il);
					if (type != args [i - 2])
						throw new CompilerError (subexprs [i], "Invalid type of arg {0}. Expected {1}, got {2}.", i - 2, args [i - 2], type);
					stlocs [i - 2] = il.DeclareLocal (getNetType (type));	
					il.Emit (OpCodes.Stloc, stlocs [i - 2]);
				}
			}

			var m = FunctionInvocation.Get (function);

			foreach (var loc in stlocs)
				il.Emit (OpCodes.Ldloc, loc);
			il.Emit (OpCodes.Call, m);

			return returnType;
		}

		public void GenerateIL (Uid expr)
		{
			
			AssemblyBuilder a = AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName("myasm"), 
				AssemblyBuilderAccess.RunAndSave);
			var module = a.DefineDynamicModule ("myasm.dll");
			var tb  = module.DefineType ("mytype", TypeAttributes.Class |TypeAttributes.Public);
			var fn = tb.DefineMethod ("run", MethodAttributes.Static | MethodAttributes.Public);

			//DynamicMethod fn = new DynamicMethod ("run", typeof(void), new Type[]{ }, typeof(IlTest).Module);
			var ilgen = fn.GetILGenerator ();
			GenSubCall (expr, ilgen);
			ilgen.Emit (OpCodes.Ret);
			Type t = tb.CreateType ();
			var runm = t.GetMethod ("run");
			Action x;
			module.CreateGlobalFunctions();
			a.Save("myasm.dll");
			//var assembly = Assembly.LoadFrom ("myasm.dll");
			//var extypes = assembly.GetExportedTypes ();
			runm.Invoke (null, null);
		}
		public delegate Uid SubExpressionDelegate(params Uid[] uids);

		public SubExpressionDelegate Sub{ get { return Expression; } }
			
		public Uid Expression(params Uid[] uids)
		{
			return CreateExpression (uids);
		}

		Dict<Uid, string> argumentName = new Dict<Uid, string>();
		Dict<Uid, Uid> argumentType = new Dict<Uid, Uid>();

		public Uid DefineArgument(string name = null, Uid type = default(Uid)){
			var id = Uid.CreateNew ();
			if (name != null)
				argumentName.Add (id, name);
			if (type != default(Uid))
				argumentType.Add (id, type);
			return id;
		}

		// Variable that defines the struct name.
		Dict<Uid, Uid> structName = new Dict<Uid, Uid>(); 
		MultiDict<Uid,Uid> structArguments = new MultiDict<Uid, Uid> ();
		public Uid DefineStruct(Uid nameVar = default(Uid), params Uid[] arguments)
		{
			var id = Uid.CreateNew ();
			if (nameVar != default(Uid))
				structName.Add (id, nameVar);
			structArguments.Add(id, arguments);
			return id;
		}

		public Uid GetStructAccessor(Uid member){
			return Uid.CreateNew ();
		}

		public Uid GetStructConstructor(Uid struct_id){
			return Uid.CreateNew ();
		}

		void GenStructConstructorIl(ILGenerator il, Uid structid){
			il.Emit (OpCodes.Ldloc, il.DeclareLocal (getNetType (structid)));
		}

		void GenStructAccessorIl(ILGenerator il, Uid structid, Uid member){
			//il.Emit(OpCodes.Ld
		}

		FieldInfo getNetFieldInfo(Uid member, Uid structType)
		{

		}

		Uid GenStructAccess(ILGenerator il, Uid[] exprs){
			Uid member = exprs [0];
			Uid structExpr = exprs [0];
			Uid structType = GenCall (structExpr, il);
			il.Emit(OpCodes.Ldfld, getNetFieldInfo(member,structType));
			return argumentType.Get (member);
		}

		public Uid GetFunctionType(Uid returnType, params Uid[] argTypes){
			return Uid.CreateNew ();
		}

		public Uid DefineFunction(string name, Uid fcnType,params Uid[] arguments){

			return Uid.CreateNew ();
		}

		public Uid DefineFcnBody(Uid fcn){
			return Uid.CreateNew ();
		}

		public Uid Progn = Uid.CreateNew();
		public Uid Add = Uid.CreateNew();
		public Uid Let(string varname, Uid expr){
			return Uid.CreateNew ();
		}

	}

}