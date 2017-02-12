using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PhotonIl;
using System.Collections;

namespace PhotonIl2
{
	static class PhotonExt{
		public static List<MethodInfo> GetMethods(this Type t, string name){
			return t.GetMethods ().Where (x => x.Name == name).ToList ();
		}
		public static List<ILispMethod> GetLispMethods(this Type t, string name){
			return t.GetMethods ().Where (x => x.Name == name).Select(x => (ILispMethod)new CsMethod(x)).ToList ();
		}
	}
	
}
