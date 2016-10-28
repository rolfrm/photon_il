using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace PhotonIl
{
	[ProtoBuf.ProtoContract]
	public class Dict<K, V> : Dictionary<K, V>{

		public V Get(K key){
			if (ContainsKey (key))
				return this [key];
			return default(V);
		}

		public K Inv(V x){
			return this.FirstOrDefault (y => y.Value.Equals (x)).Key;
		}

		public Dict() : base(){

		}

		public Dict<K,V2> ConvertValues<V2>(Func<V,V2> f){
			Dict<K,V2> @new = new Dict<K, V2> ();
			foreach (var x in this) {
				@new [x.Key] = f (x.Value);
			}
			return @new;
		}
	}

	public static class DictExtension
	{
		public static Dict<Uid, T> LocalOnly<T>(this Dict<Uid, T> self){
			Dict<Uid, T> d = new Dict<Uid, T> ();
			foreach (var x in self)
				if (x.Key.AssemblyId == 0)
					d [x.Key] = x.Value;
			return d;
		}

		public static MultiDict<Uid, T> LocalOnly<T>(this MultiDict<Uid, T> self){
			MultiDict<Uid, T> d = new MultiDict<Uid, T> ();
			foreach (var x in self.Entries)
				if (x.Key.AssemblyId == 0)
					d.Add(x.Key, x.Value);
			return d;
		}
	}

	[ProtoContract]
	public class MultiDict<K, V>{
		[ProtoMember(1)]
		Dictionary<K, List<V>> dict = new Dictionary<K, List<V>>();
		public void Add(K key, V value){
			if(dict.ContainsKey(key) == false)
				dict[key] = new List<V>{};
			dict [key].Add (value);
		}
		public void Add(K key, IEnumerable<V> value){
			if (key.Equals(default(K)))
				throw new Exception ("key cannot be the default value");
			if(dict.ContainsKey(key) == false)
				dict[key] = new List<V>{};
			dict [key].AddRange (value);
		}
		static readonly List<V> empty = new List<V>();
		public List<V> Get(K key){
			if (dict.ContainsKey (key))
				return dict [key];
			return empty.ToList();
		}

		public bool Contains(K key){
			return dict.ContainsKey (key);
		}

		public void Remove(K key){
			dict.Remove (key);
		}

		public IEnumerable<KeyValuePair<K, List<V>>> Entries{
			get{ return dict; }
		}
	}
}

