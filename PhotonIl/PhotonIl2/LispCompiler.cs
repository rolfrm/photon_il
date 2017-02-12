using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace PhotonIl2
{
	public class LispCompiler
	{
		public LispCompiler ()
		{
		}

		AssemblyBuilder builder;
		ModuleBuilder modBuilder;
		string AssemblyName(){
			return "testcc.dll";
		}
		ModuleBuilder getDynamicModule ()
		{
			if (builder == null) {

				builder = AppDomain.CurrentDomain.DefineDynamicAssembly (
					new AssemblyName (AssemblyName()),
					AssemblyBuilderAccess.RunAndCollect, System.IO.Directory.GetCurrentDirectory ());
				modBuilder = builder.DefineDynamicModule (AssemblyName(),AssemblyName(),true);
				//CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder (typeof(System.Runtime.InteropServices.GuidAttribute).GetConstructors()[0], new object[]{AssemblyName()});
				//builder.SetCustomAttribute (attrBuilder);
			}
			return modBuilder;
		}

		public void CompileFunction(Symbol name, Cons arguments, params object[] body)
		{
			var module = getDynamicModule ();
			var tp = module.DefineType ("cl-user");

			foreach (var arg in arguments) {

			}

			var mb = tp.DefineMethod (name.String, MethodAttributes.Public | MethodAttributes.Static
				, typeof(int), new Type[0]);
			var gen = mb.GetILGenerator ();

		}

	}
}

