﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ConsoleApplication1 {

	public class MonoHeapState {

		public Dictionary<ulong, MonoClass> PtrClassMapping = new Dictionary<ulong, MonoClass>();
		public Dictionary<ulong, MonoMethod> PtrMethodMapping = new Dictionary<ulong, MonoMethod>();
		public Dictionary<ulong, BackTrace> PtrBackTraceMapping = new Dictionary<ulong, BackTrace>();
		public Dictionary<ulong, ulong> PtrBacktraceToPtrClass = new Dictionary<ulong, ulong>();

		public Dictionary<ulong, MonoObjectNew> ObjectPtrToMonoObjectNew = new Dictionary<ulong, MonoObjectNew>();

		public List<GarbageCollection> GarbageCollections = new List<GarbageCollection>();

		private Dictionary<ulong, LiveObject> _liveObjects = new Dictionary<ulong, LiveObject>();

		private struct LiveObject {

			public MonoClass Class;
			public MonoMethod Method;
			public ulong BackTracePtr;

			public ulong ObjectPtr;
			public uint Size;
			public bool IsStatic;
		}

		private ulong? ObjectClassPtr = null;

		//public IEnumerable<string> Get

		public void PostInitialize() {

			ObjectClassPtr = PtrClassMapping.FirstOrDefault( _ => _.Value.Name == "object" ).Key;
		}
		
		public void AddLiveObject( MonoObjectNew newObject ) {

			if ( ObjectClassPtr == null ) {

				ObjectClassPtr = PtrClassMapping.FirstOrDefault( _ => _.Value.Name == "object" ).Key;
			}

			var liveObject = new LiveObject();

			var backtrace = PtrBackTraceMapping[newObject.BackTracePtr];
			var index = Math.Max( backtrace.StackFrames.Length - 2, 0 );
			var stackFrame = backtrace.StackFrames.Length > 0 ? backtrace.StackFrames[index] : null;

			if ( stackFrame != null ) {
				var referencingMethodPtr = stackFrame.MethodPtr;
				var referencingMethod = PtrMethodMapping.ContainsKey( referencingMethodPtr ) ? PtrMethodMapping[referencingMethodPtr] : default( MonoMethod );
				liveObject.Method = referencingMethod;
			}

			liveObject.Class = PtrClassMapping[newObject.ClassPtr];
			liveObject.ObjectPtr = newObject.ObjectPtr;
			liveObject.BackTracePtr = newObject.BackTracePtr;

			liveObject.Size = newObject.Size;
			liveObject.IsStatic = false;

			_liveObjects.Add( liveObject.ObjectPtr, liveObject );
		}

		public void AddLiveObject( MonoStaticClassAllocation newObject ) {

			if ( ObjectClassPtr == null ) {

				ObjectClassPtr = PtrClassMapping.FirstOrDefault( _ => _.Value.Name == "object" ).Key;
			}

			var liveObject = new LiveObject();

			liveObject.Class = PtrClassMapping[newObject.ClassPtr];
			liveObject.Method = null;
			liveObject.ObjectPtr = newObject.ObjectPtr;

			liveObject.Size = newObject.Size;
			liveObject.IsStatic = true;

			_liveObjects.Add( liveObject.ObjectPtr, liveObject );
		}

		public void RemoveLiveObject(MonoObjectGarbageCollected collectedObject) {

			_liveObjects.Remove( collectedObject.ObjectPtr );
		}

		public float GetTotalLiveObjectsSizeMb() {

			return _liveObjects.Values.Aggregate( 0f, ( total, each ) => total + each.Size ) / ( 1024 * 1024 );
		}

		public List<Tuple<string, string>> GetLiveObjects() {

			var objectMonoClass = PtrClassMapping.FirstOrDefault( _ => _.Value.Name == "object" );

			Console.WriteLine( "object class ptr = " + objectMonoClass.Key );

			var result = new List<Tuple<string, string>>();
			var garbageCollection = GarbageCollections.Last();

			foreach(var each in _liveObjects) {

				var monoClass = each.Value.Class;

				if ( monoClass.Name.Contains("Reflection") || monoClass.Name.Contains("MonoType")) { continue; }

				//var backtrace = PtrBackTraceMapping[each.Value.b];
				//var stackFrame = backtrace.StackFrames.FirstOrDefault( _ => PtrMethodMapping[_.MethodPtr].ClassPtr != objectMonoClass.Key );

				var referencingClassName = string.Empty;

				//if (stackFrame != null)
					{
					//var referencingMethodPtr = stackFrame.MethodPtr;
					//var referencingMethod = PtrMethodMapping.ContainsKey(referencingMethodPtr) ? PtrMethodMapping[referencingMethodPtr] : default(MonoMethod);

					var referencingMethod = each.Value.Method;

					if ( referencingMethod != null) {
						var referencingClass = PtrClassMapping.ContainsKey( referencingMethod.ClassPtr) ? PtrClassMapping[referencingMethod.ClassPtr] : null;

						if (referencingClass != null ) {
							referencingClassName = referencingClass.Name;

						}
					}

					if ( each.Value.Method == null ) {

						referencingClassName = "Mono Runtime Reference";
					}
				}

				
				
				//var liveObject = new {

				//	Name = PtrClassMapping[each.Value.ClassPtr].Name,
				//	ReferencedBy = referencingClassName
				//};

				result.Add( new Tuple<string, string>( monoClass.Name, referencingClassName ) );
			}

			return result;
		}

		public IEnumerable<Tuple<string, string, uint>> GetObjectReferencesWithSize() {

			var mapping = new Dictionary<MonoMethod, Dictionary<MonoClass, uint>>();

			foreach(var each in _liveObjects) {

				if (each.Value.Class == null || each.Value.Method == null) { continue; }

				var classToAllocationSizeMapping = default( Dictionary<MonoClass, uint> );

				if (!mapping.TryGetValue(each.Value.Method, out classToAllocationSizeMapping ) ) {

					classToAllocationSizeMapping = mapping[each.Value.Method] = new Dictionary<MonoClass, uint>();
				}

				var allocationSize = 0u;
				classToAllocationSizeMapping.TryGetValue(each.Value.Class, out allocationSize);
				classToAllocationSizeMapping[each.Value.Class] = allocationSize + each.Value.Size;
			}

			foreach (var eachAllocationMapping in mapping) {

				var monoMethod = eachAllocationMapping.Key;

				if ( !PtrClassMapping.ContainsKey( monoMethod.ClassPtr ) ) { continue; }

				var allocationSource = PtrClassMapping[monoMethod.ClassPtr].Name + "." + monoMethod.Name;

				foreach (var eachSizeMapping in eachAllocationMapping.Value) {

					yield return new Tuple<string, string, uint>(allocationSource, eachSizeMapping.Key.Name, eachSizeMapping.Value);
				}
			}
		}

		public void DumpMethodAllocationStats( TextWriter writer ) {

			var backtraceToString = new Dictionary<ulong, string>();

			foreach ( var each in PtrBackTraceMapping ) {

				var backtraceString = BackTraceToString( each.Value );

				backtraceToString[each.Key] = backtraceString;
			}

			if ( !backtraceToString.ContainsKey( 0 ) ) {

				backtraceToString[0] = string.Empty;
			}

			//foreach ( var each in _liveObjects ) {

			//	writer.WriteLine( each.Value.Class.Name + " " + each.Value.Size + " Static: " + each.Value.IsStatic );
			//}

			var statsBySize = new Dictionary<MonoClass, uint>();
			foreach ( var each in _liveObjects ) {
				
				if ( !statsBySize.ContainsKey( each.Value.Class ) ) {

					statsBySize[each.Value.Class] = 0;
				}

				statsBySize[each.Value.Class] += each.Value.Size;
			}

			var statsBySizeList = statsBySize.ToList();
			statsBySizeList.Sort( ( a, b ) => b.Value.CompareTo( a.Value ) );

			var sizeTotalMb = 0f;
			foreach ( var each in statsBySizeList ) {
				sizeTotalMb += ( (float)each.Value / ( 1024 * 1024 ) );
				writer.WriteLine( each.Key.Name + " " + ((float)each.Value / (1024 * 1024)) + " MB");
			}

			Console.WriteLine( sizeTotalMb + " MB" );
		}

		private string BackTraceToString(BackTrace backtrace) {

			var result = new StringBuilder();
			foreach (var each in backtrace.StackFrames) {

				var monoMethod = PtrMethodMapping.ContainsKey( each.MethodPtr ) ? PtrMethodMapping[each.MethodPtr] : null;

				if (monoMethod == null) {

					continue;
				}

				result.Append( monoMethod.Name );
				result.Append( "->" );
			}

			return result.ToString();
		}
	}
}