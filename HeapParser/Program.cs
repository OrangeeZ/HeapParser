using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace ConsoleApplication1 {

	public abstract class HeapDescriptor {

		public abstract void LoadFrom( BinaryReader inputStream );

		public virtual void ApplyTo(MonoHeapState monoHeapState ) {

		}
	}

	[Serializable]
	public class FileWriterStats : HeapDescriptor {

		public uint FileSignature;
		public int FileVersion;
		public string FileLabel;
		public ulong Timestamp;
		public byte PointerSize;
		public byte PlatformId;
		public bool IsLogFullyWritten;

		public override void LoadFrom( BinaryReader inputStream ) {

			FileSignature = inputStream.ReadUInt32();
			FileVersion = inputStream.ReadInt32();

			var stringLength = inputStream.ReadUInt32();
			var chars = inputStream.ReadChars( (int)stringLength );
			FileLabel = new string( chars );

			Timestamp = inputStream.ReadUInt64();
			PointerSize = inputStream.ReadByte();
			PlatformId = inputStream.ReadByte();

			IsLogFullyWritten = inputStream.ReadBoolean();
		}
	}

	public class HeapDumpStats : HeapDescriptor {
		public uint TotalGcCount;
		public uint TotalTypeCount;
		public uint TotalMethodCount;
		public uint TotalBacktraceCount;
		public uint TotalResizeCount;

		public ulong TotalFramesCount;
		public ulong TotalObjectNewCount;
		public ulong TotalObjectResizesCount;
		public ulong TotalObjectGcsCount;
		public ulong TotalBoehmNewCount;
		public ulong TotalBoehmFreeCount;

		public ulong TotalAllocatedBytes;
		public uint TotalAllocatedObjects;

		public override void LoadFrom( BinaryReader inputStream ) {
			TotalGcCount = inputStream.ReadUInt32();
			TotalTypeCount = inputStream.ReadUInt32();
			TotalMethodCount = inputStream.ReadUInt32();
			TotalBacktraceCount = inputStream.ReadUInt32();
			TotalResizeCount = inputStream.ReadUInt32();

			TotalFramesCount = inputStream.ReadUInt64();
			TotalObjectNewCount = inputStream.ReadUInt64();
			TotalObjectResizesCount = inputStream.ReadUInt64();
			TotalObjectGcsCount = inputStream.ReadUInt64();
			TotalBoehmNewCount = inputStream.ReadUInt64();
			TotalBoehmFreeCount = inputStream.ReadUInt64();

			TotalAllocatedBytes = inputStream.ReadUInt64();
			TotalAllocatedObjects = inputStream.ReadUInt32();
		}
	}

	[Serializable]
	public class HeapMemoryStart : HeapDescriptor {
		public uint WrittenSectionsCount;
		public uint MemoryTotalHeapBytes;
		public uint MemoryTotalBytesWritten;
		public uint MemoryTotalRoots;
		public uint MemoryTotalThreads;

		public HeapMemorySection[] HeapMemorySections;

		public HeapMemoryRootSet[] HeapMemoryStaticRoots;

		public HeapMemoryThread[] HeapMemoryThreads;

		public override void LoadFrom(BinaryReader inputStream) {

			WrittenSectionsCount = inputStream.ReadUInt32();
			MemoryTotalHeapBytes = inputStream.ReadUInt32();
			MemoryTotalBytesWritten = inputStream.ReadUInt32();
			MemoryTotalRoots = inputStream.ReadUInt32();
			MemoryTotalThreads = inputStream.ReadUInt32();

			HeapMemorySections = new HeapMemorySection[WrittenSectionsCount];

			for (var i = 0; i < WrittenSectionsCount; ++i ) {

				EnsureTag( (HeapTag)inputStream.ReadByte(), HeapTag.cTagHeapMemorySection );

				HeapMemorySections[i] = new HeapMemorySection();
				HeapMemorySections[i].LoadFrom( inputStream );
			}

			EnsureTag( (HeapTag)inputStream.ReadByte(), HeapTag.cTagHeapMemoryRoots );

			HeapMemoryStaticRoots = new HeapMemoryRootSet[MemoryTotalRoots];

			for (var i = 0; i < MemoryTotalRoots; ++i ) {

				HeapMemoryStaticRoots[i] = new HeapMemoryRootSet();
				HeapMemoryStaticRoots[i].LoadFrom( inputStream );
			}

			EnsureTag( (HeapTag)inputStream.ReadByte(), HeapTag.cTagHeapMemoryThreads );

			HeapMemoryThreads = new HeapMemoryThread[MemoryTotalThreads];
			
			for (var i = 0; i < MemoryTotalThreads;++i ) {

				HeapMemoryThreads[i] = new HeapMemoryThread();
				HeapMemoryThreads[i].LoadFrom( inputStream );
			}

			EnsureTag( (HeapTag)inputStream.ReadByte(), HeapTag.cTagHeapMemoryEnd );
		}

		private static void EnsureTag(HeapTag currentTag, HeapTag expectedTag ) {
			
			if ( currentTag != expectedTag ) {

				throw new Exception( string.Format( "Heap dump read error: got {0} expected {1}", currentTag, expectedTag ) );
			}
		}
	}

	[Serializable]
	public class HeapMemorySection : HeapDescriptor {
		public ulong StartPtr;
		public ulong EndPtr;
		public uint BlocksWrittenCount;

		public HeapSectionBlock[] HeapSectionBlocks;

		public override void LoadFrom( BinaryReader inputStream ) {

			StartPtr = inputStream.ReadUInt64();
			EndPtr = inputStream.ReadUInt64();
			BlocksWrittenCount = inputStream.ReadUInt32();

			HeapSectionBlocks = new HeapSectionBlock[BlocksWrittenCount];
			for (var i = 0; i < BlocksWrittenCount; ++i ) {

				EnsureTag( (HeapTag)inputStream.ReadByte(), HeapTag.cTagHeapMemorySectionBlock );

				HeapSectionBlocks[i] = new HeapSectionBlock();
				HeapSectionBlocks[i].LoadFrom( inputStream );
			}
		}

		private static void EnsureTag( HeapTag currentTag, HeapTag expectedTag ) {

			if ( currentTag != expectedTag ) {

				throw new Exception( string.Format( "Heap dump read error: got {0} expected {1}", currentTag, expectedTag ) );
			}
		}
	}

	[Serializable]
	public class HeapSectionBlock : HeapDescriptor {
		public ulong StartPtr;
		public uint Size;
		public uint ObjSize;
		public byte BlockKind;
		public bool IsFree;
		public ulong[] ObjectPtrs;

		public override void LoadFrom( BinaryReader inputStream ) {

			StartPtr = inputStream.ReadUInt64();
			Size = inputStream.ReadUInt32();
			ObjSize = inputStream.ReadUInt32();
			BlockKind = inputStream.ReadByte();
			IsFree = inputStream.ReadBoolean();

			if (!IsFree) {

				var ptrSize = 4;
				var ptrCount = Size / ptrSize;
				ObjectPtrs = new ulong[ptrCount];

				for ( var i = 0; i < ptrCount; ++i ) {

					ObjectPtrs[i] = inputStream.ReadUInt32();
				}
			}
		}
	}

	public class HeapMemoryRootSet : HeapDescriptor {

		public ulong StartPtr;
		public ulong EndPtr;
		public uint Size;
		public ulong[] ObjectPtrs; // TODO: check what actually lies in root sets

		public override void LoadFrom( BinaryReader inputStream ) {

			StartPtr = inputStream.ReadUInt64();
			EndPtr = inputStream.ReadUInt64();
			Size = inputStream.ReadUInt32();

			var ptrSize = 4;
			var ptrCount = Size / ptrSize;
			ObjectPtrs = new ulong[ptrCount];

			for (var i = 0; i < ptrCount; ++i ) {

				ObjectPtrs[i] = inputStream.ReadUInt32();
			}
		}
	}

	public class HeapMemoryThread : HeapDescriptor {

		public int ThreadId;
		public ulong StackPtr;
		public uint StackSize;
		public uint RegistersSize;

		public byte[] RawStackMemory;
		public byte[] RawRegistersMemory;

		public override void LoadFrom( BinaryReader inputStream ) {

			ThreadId = inputStream.ReadInt32();
			StackPtr = inputStream.ReadUInt64();
			StackSize = inputStream.ReadUInt32();
			RegistersSize = inputStream.ReadUInt32();

			RawStackMemory = inputStream.ReadBytes( (int)StackSize );
			RawRegistersMemory = inputStream.ReadBytes( (int)RegistersSize );
		}
	}

	public class BoehmAllocation : HeapDescriptor {

		public ulong Timestamp;
		public ulong AllocatedObjectPtr;
		public uint Size;
		public uint StacktraceHash;

		public override void LoadFrom( BinaryReader inputStream ) {
			Timestamp = inputStream.ReadUInt64();
			AllocatedObjectPtr = inputStream.ReadUInt64();
			Size = inputStream.ReadUInt32();
			StacktraceHash = inputStream.ReadUInt32();
		}
	}

	public class BoehmAllocationStacktrace : HeapDescriptor {

		public uint StacktraceHash;
		public string StacktraceBuffer;

		public override void LoadFrom( BinaryReader inputStream ) {
			StacktraceHash = inputStream.ReadUInt32();

			var stringLength = inputStream.ReadUInt32();
			var chars = inputStream.ReadChars( (int)stringLength );
			StacktraceBuffer = new string( chars );
		}
	}

	public class GarbageCollectionAccountant : HeapDescriptor {

		public ulong BackTracePtr;
		public ulong ClassPtr;
		public uint NumberOfAllocatedObjects;
		public ulong NunberOfAllocatedBytes;
		public uint AllocatedTotalAge;
		public uint AllocatedTotalWeight;
		public uint NumberOfLiveObjects;
		public uint NumberOfLiveBytes;
		public uint LiveTotalAge;
		public uint LiveTotalWeight;

		public override void LoadFrom( BinaryReader inputStream ) {

			BackTracePtr = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
			NumberOfAllocatedObjects = inputStream.ReadUInt32();
			NunberOfAllocatedBytes = inputStream.ReadUInt64();
			AllocatedTotalAge = inputStream.ReadUInt32();
			AllocatedTotalWeight = inputStream.ReadUInt32();
			NumberOfLiveObjects = inputStream.ReadUInt32();
			NumberOfLiveBytes = inputStream.ReadUInt32();
			LiveTotalAge = inputStream.ReadUInt32();
			LiveTotalWeight = inputStream.ReadUInt32();
		}
	}

	public class GarbageCollection : HeapDescriptor {

		public int TotalGcCount;
		public ulong Timestamp;
		public ulong TotalLiveBytesBefore;
		public uint TotalLiveObjectsBefore;
		public ulong TotalLiveBytesAfter;
		public uint TotalLiveObjectsAfter;
		public uint NumberOfAccountants;
		public GarbageCollectionAccountant[] Accountants;

		public override void LoadFrom( BinaryReader inputStream ) {

			TotalGcCount = inputStream.ReadInt32();
			Timestamp = inputStream.ReadUInt64();
			TotalLiveBytesBefore = inputStream.ReadUInt64();
			TotalLiveObjectsBefore = inputStream.ReadUInt32();
			NumberOfAccountants = inputStream.ReadUInt32();


			Accountants = new GarbageCollectionAccountant[NumberOfAccountants];
			for(var i = 0; i < NumberOfAccountants; ++i) {

				Accountants[i] = new GarbageCollectionAccountant();
				Accountants[i].LoadFrom( inputStream );
			}

			TotalLiveBytesAfter = inputStream.ReadUInt64();
			TotalLiveObjectsAfter = inputStream.ReadUInt32();
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {

			monoHeapState.GarbageCollections.Add( this );
		}
	}

	public class HeapSizeStats : HeapDescriptor {
		public ulong HeapSize;
		public ulong HeapUsedSize;

		public override void LoadFrom( BinaryReader inputStream ) {
			HeapSize = inputStream.ReadUInt64();
			HeapUsedSize = inputStream.ReadUInt64();
		}
	}

	public class MonoClass : HeapDescriptor {

		public ulong ClassPtr;
		public string Name;
		public byte Flags;
		public uint Size;
		public uint MinAlignment;

		public override void LoadFrom( BinaryReader inputStream ) {
			ClassPtr = inputStream.ReadUInt64();

			var stringLength = inputStream.ReadUInt32();
			var chars = inputStream.ReadChars( (int)stringLength );
			Name = new string( chars );
			
			Flags = inputStream.ReadByte();
			Size = inputStream.ReadUInt32();
			MinAlignment = inputStream.ReadUInt32();
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {

			monoHeapState.PtrClassMapping[ClassPtr] = this;
		}
	}

	public class MonoVTableCreate : HeapDescriptor{
		public ulong Timestamp;
		public ulong VTablePtr;
		public ulong ClassPtr;

		public override void LoadFrom( BinaryReader inputStream ) {
			Timestamp = inputStream.ReadUInt64();
			VTablePtr = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
		}
	}

	public class BackTraceStackFrame : HeapDescriptor {
		public ulong MethodPtr;
		public uint DebugLine;

		public override void LoadFrom( BinaryReader inputStream ) {
			MethodPtr = inputStream.ReadUInt64();
			DebugLine = inputStream.ReadUInt32();
		}
	}

	public class BackTrace : HeapDescriptor {
		public ulong BackTracePtr;
		public ulong Timestamp;
		public short StackFrameCount;

		public BackTraceStackFrame[] StackFrames;

		public override void LoadFrom( BinaryReader inputStream ) {
			BackTracePtr = inputStream.ReadUInt64();
			Timestamp = inputStream.ReadUInt64();
			StackFrameCount = inputStream.ReadInt16();

			StackFrames = new BackTraceStackFrame[StackFrameCount];
			for (var i = 0; i < StackFrameCount; ++i) {

				StackFrames[i] = new BackTraceStackFrame();
				StackFrames[i].LoadFrom( inputStream );
			}
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {
			monoHeapState.PtrBackTraceMapping[BackTracePtr] = this;
		}
	}

	class BackTraceTypeLink : HeapDescriptor {
		public ulong BackTracePtr;
		public ulong ClassPtr;

		public override void LoadFrom( BinaryReader inputStream ) {
			BackTracePtr = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {
			monoHeapState.PtrBacktraceToPtrClass[BackTracePtr] = ClassPtr;
		}
	}

	public class MonoObjectNew : HeapDescriptor {
		public ulong Timestamp;
		public ulong BackTracePtr;
		public ulong ClassPtr;
		public ulong ObjectPtr;
		public uint Size;

		public override void LoadFrom( BinaryReader inputStream ) {
			Timestamp = inputStream.ReadUInt64();
			BackTracePtr = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
			ObjectPtr = inputStream.ReadUInt64();
			Size = inputStream.ReadUInt32();
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {

			monoHeapState.AddLiveObject( this );
		}
	}

	public class MonoMethod : HeapDescriptor {
		public ulong MethodPtr;
		public ulong ClassPtr;
		public string Name;
		public string SourceFileName;

		public override void LoadFrom( BinaryReader inputStream ) {

			MethodPtr = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
			
			var stringLength = inputStream.ReadUInt32();
			var chars = inputStream.ReadChars( (int)stringLength );
			Name = new string( chars );

			stringLength = inputStream.ReadUInt32();
			chars = inputStream.ReadChars( (int)stringLength );
			SourceFileName = new string( chars );
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {

			monoHeapState.PtrMethodMapping[MethodPtr] = this;
		}
	}

	public class MonoObjectGarbageCollected : HeapDescriptor {
		public ulong BackTracePtr;
		public ulong ClassPtr;
		public ulong ObjectPtr;

		public override void LoadFrom( BinaryReader inputStream ) {
			BackTracePtr = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
			ObjectPtr = inputStream.ReadUInt64();
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {

			monoHeapState.RemoveLiveObject( this );
		}
	}

	class MonoHeapResize : HeapDescriptor {
		public ulong Timestamp;
		public ulong NewSize;
		public ulong TotalLiveBytes;
		public uint TotalLiveObjects;

		public override void LoadFrom( BinaryReader inputStream ) {
			Timestamp = inputStream.ReadUInt64();
			NewSize = inputStream.ReadUInt64();
			TotalLiveBytes = inputStream.ReadUInt64();
			TotalLiveObjects = inputStream.ReadUInt32();
		}
	}

	class MonoObjectResize : HeapDescriptor {
		public ulong BackTracePtr;
		public ulong ClassPtr;
		public ulong ObjectPtr;
		public uint Size;

		public override void LoadFrom( BinaryReader inputStream ) {
			BackTracePtr = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
			ObjectPtr = inputStream.ReadUInt64();
			Size = inputStream.ReadUInt32();
		}
	}

	public class CustomEvent : HeapDescriptor {
		public ulong Timestamp;
		public string EventName;

		public override void LoadFrom( BinaryReader inputStream ) {

			Timestamp = inputStream.ReadUInt64();

			var stringLength = inputStream.ReadUInt32();
			var chars = inputStream.ReadChars( (int)stringLength );
			EventName = new string( chars );
		}
	}

	public class MonoStaticClassAllocation : HeapDescriptor {

		public ulong Timestamp;
		public ulong ClassPtr;
		public ulong ObjectPtr; //TODO: verify this; from callstack it does look like static object ref, but who knows
		public uint Size;

		public override void LoadFrom( BinaryReader inputStream ) {

			Timestamp = inputStream.ReadUInt64();
			ClassPtr = inputStream.ReadUInt64();
			ObjectPtr = inputStream.ReadUInt64();
			Size = inputStream.ReadUInt32();
		}

		public override void ApplyTo( MonoHeapState monoHeapState ) {

			monoHeapState.AddLiveObject( this );
		}
	}

	public class MonoThreadTableResize : HeapDescriptor {

		public ulong Timestamp;
		public ulong TablePtr;
		public uint TableCount;
		public uint TableSize;

		public override void LoadFrom( BinaryReader inputStream ) {
			Timestamp = inputStream.ReadUInt64();
			TablePtr = inputStream.ReadUInt64();
			TableCount = inputStream.ReadUInt32();
			TableSize = inputStream.ReadUInt32();
		}
	}
	
	public class MonoThreadStaticClassAllocation : HeapDescriptor { //TODO: verify that this is indeed allocation of statics by threads

		public ulong Timestamp;
		public ulong ObjectPtr; //TODO: verify this; from callstack it does look like static object ref, but who knows
		public uint Size;

		public override void LoadFrom( BinaryReader inputStream ) {
			Timestamp = inputStream.ReadUInt64();
			ObjectPtr = inputStream.ReadUInt64();
			Size = inputStream.ReadUInt32();
		}
	}

	enum HeapTag {
		cFileSignature = 0x4EABB055,
		cFileVersion = 3,

		cTagNone = 0,

		cTagType,
		cTagMethod,
		cTagBackTrace,
		cTagGarbageCollect,
		cTagResize,
		cTagMonoObjectNew,
		cTagMonoObjectSizeChange,
		cTagMonoObjectGc,
		cTagHeapSize,
		cTagHeapMemoryStart,
		cTagHeapMemoryEnd,
		cTagHeapMemorySection,
		cTagHeapMemorySectionBlock,
		cTagHeapMemoryRoots,
		cTagHeapMemoryThreads,
		cTagBoehmAlloc,
		cTagBoehmFree,
		cTagMonoVTable,
		cTagMonoClassStatics,
		cTagMonoThreadTableResize,
		cTagMonoThreadStatics,
		cTagBackTraceTypeLink,
		cTagBoehmAllocStacktrace,

		cTagEos = byte.MaxValue,

		cTagCustomEvent = cTagEos - 1,
		cTagAppResignActive = cTagEos - 2,
		cTagAppBecomeActive = cTagEos - 3,
		cTagNewFrame = cTagEos - 4,
		cTagAppMemoryStats = cTagEos - 5,
	};

	class HeapTagParser {

		private Stream _inputStream;
		private List<HeapDescriptor> _heapDescriptors;
		private List<CustomEvent> _heapCustomEvents;
		private List<GarbageCollection> _garbageCollectionEvents;

		public GarbageCollection TargetGcEvent { get; private set; }

		public HeapTagParser( Stream inputStream ) {

			_inputStream = inputStream;
		}

		public void ParseHeapDump() {

			_heapDescriptors = new List<HeapDescriptor>();
			_heapCustomEvents = new List<CustomEvent>();
			_garbageCollectionEvents = new List<GarbageCollection>();

			var binaryReader = new BinaryReader( _inputStream, Encoding.UTF8 );
			var serializer = JsonSerializer.Create();

			var writerStats = new FileWriterStats();
			writerStats.LoadFrom( binaryReader );

			_heapDescriptors.Add( writerStats );

			Console.WriteLine( JsonConvert.SerializeObject( writerStats ) );

			var dumpStats = new HeapDumpStats();
			dumpStats.LoadFrom( binaryReader );

			_heapDescriptors.Add( dumpStats );

			Console.WriteLine( JsonConvert.SerializeObject( dumpStats ) );

			var lastTag = HeapTag.cTagNone;

			var isEos = false;
			
			while ( binaryReader.BaseStream.Position != binaryReader.BaseStream.Length && !isEos ) {

				var tag = (HeapTag)_inputStream.ReadByte();
				var descriptor = default( HeapDescriptor );

				switch ( tag ) {

					case HeapTag.cTagHeapMemoryStart:
						descriptor = new HeapMemoryStart();
						break;

					case HeapTag.cTagType:
						descriptor = new MonoClass();
						break;

					case HeapTag.cTagBoehmAlloc:
						descriptor = new BoehmAllocation();
						break;

					case HeapTag.cTagBoehmAllocStacktrace:
						descriptor = new BoehmAllocationStacktrace();
						break;

					case HeapTag.cTagGarbageCollect:
						var gcEvent = new GarbageCollection();
						_garbageCollectionEvents.Add( gcEvent );
						descriptor = gcEvent;
						break;

					case HeapTag.cTagHeapSize:
						descriptor = new HeapSizeStats();
						break;

					case HeapTag.cTagMonoVTable:
						descriptor = new MonoVTableCreate();
						break;

					case HeapTag.cTagBackTrace:
						descriptor = new BackTrace();
						break;

					case HeapTag.cTagBackTraceTypeLink:
						descriptor = new BackTraceTypeLink();
						break;

					case HeapTag.cTagMonoObjectNew:
						descriptor = new MonoObjectNew();
						break;

					case HeapTag.cTagMethod:
						descriptor = new MonoMethod();
						break;

					case HeapTag.cTagMonoObjectGc:
						descriptor = new MonoObjectGarbageCollected();
						break;

					case HeapTag.cTagResize:
						descriptor = new MonoHeapResize();
						break;

					case HeapTag.cTagMonoObjectSizeChange:
						descriptor = new MonoObjectResize();
						break;

					case HeapTag.cTagCustomEvent:
						var customEvent = new CustomEvent();
						_heapCustomEvents.Add( customEvent );
						descriptor = customEvent;
						break;

					case HeapTag.cTagMonoClassStatics:
						descriptor = new MonoStaticClassAllocation();
						break;

					case HeapTag.cTagMonoThreadTableResize:
						descriptor = new MonoThreadTableResize();
						break;

					case HeapTag.cTagMonoThreadStatics:
						descriptor = new MonoThreadStaticClassAllocation();
						break;

					case HeapTag.cTagEos:
						Console.WriteLine( "End of stream" );
						isEos = true;
						break;

					default:
						Console.WriteLine( "Previous tag: " + lastTag );
						Console.WriteLine( "Tag " + tag + " unsupported; halting" );
						return;
				}

				try {
					if ( !isEos && descriptor != null ) {

						descriptor.LoadFrom( binaryReader );

						_heapDescriptors.Add( descriptor );
					}

					lastTag = tag;
				} catch (Exception e){

					Console.WriteLine( "Previous tag: " + lastTag );
					Console.WriteLine( "Current tag: " + tag );
					Console.WriteLine( e );

				}
			}

			TargetGcEvent = _garbageCollectionEvents[_garbageCollectionEvents.Count - 8];
			Console.WriteLine( TargetGcEvent.Timestamp );
		}

		public List<HeapDescriptor> GetHeapDescriptors() {

			return _heapDescriptors;
		}

		public IList<CustomEvent> GetCustomEvents() {

			return _heapCustomEvents;
		}
	}

	class Program {

		private static CustomEvent SelectTargetCustomEvent(IList<CustomEvent> customEvents) {

			if (customEvents.Count == 0) {
				return null;
			}

			var resultIndex = -1;

			Console.WriteLine( "Select event to replay heap to: " );

			while (resultIndex < 0 || resultIndex >= customEvents.Count) {

				for (var i = 0; i < customEvents.Count; ++i ) {

					Console.WriteLine( i.ToString() + " " + customEvents[i].EventName );
				}

				int.TryParse( Console.ReadLine(), out resultIndex );
			}

			return customEvents[resultIndex];
		}

		static void Main( string[] args ) {

			using ( var stream = new FileStream( args[0], FileMode.Open ) ) {

				var heapTagParser = new HeapTagParser( stream );

				heapTagParser.ParseHeapDump();


				var monoHeapState = new MonoHeapState();

				var targetCustomEvent = SelectTargetCustomEvent( heapTagParser.GetCustomEvents() );

				foreach ( var each in heapTagParser.GetHeapDescriptors() ) {

					if ( each == targetCustomEvent || each == heapTagParser.TargetGcEvent ) {

						Console.WriteLine( "Hit " + each.ToString() );

						break;
					}

					each.ApplyTo( monoHeapState );
				}

				monoHeapState.PostInitialize();

				using ( var outStream = new FileStream( args[0] + ".out.txt", FileMode.Create ) ) {

					var streamWriter = new StreamWriter( outStream );
					var writer = new JsonTextWriter( streamWriter );

					monoHeapState.DumpMethodAllocationStats( streamWriter );

					//var serializer = JsonSerializer.Create();

					//foreach ( var each in heapTagParser.GetHeapDescriptors() ) {

					//	writer.WriteComment( each.GetType().Name );
					//	serializer.Serialize( writer, each );
					//	writer.WriteRaw( "\n" );
					//}

					writer.Close();
				}

				//using ( var outStream = new FileStream( args[0] + ".heap.out.txt", FileMode.Create ) ) {

				//	var streamWriter = new StreamWriter( outStream );
				//	var writer = new JsonTextWriter( streamWriter );
				//	var serializer = JsonSerializer.Create();

				//	var monoHeapState = new MonoHeapState();
				//	foreach(var each in heapTagParser.GetHeapDescriptors()) {

				//		if (each is CustomEvent ) {
				//			break;
				//		}

				//		each.ApplyTo( monoHeapState );
				//	}

				//	var liveObjects = monoHeapState.GetLiveObjects();

				//	foreach ( var each in liveObjects ) {

				//		//writer.WriteComment( each.GetType().Name );
				//		serializer.Serialize( writer, each );
				//		writer.WriteRaw( "\n" );
				//	}

				//	writer.Close();
				//}

				using ( var outStream = new FileStream( args[0] + ".heap.graph.gdf", FileMode.Create ) ) {

					var streamWriter = new StreamWriter( outStream );
					///var writer = new TextWriter( streamWriter );
					var serializer = JsonSerializer.Create();



					//foreach (var each in monoHeapState.GetLiveObjectsNew()) {

					//}

					var gdfGenerator = new GdfGenerator();
					//var liveObjects = monoHeapState.GetLiveObjects();

					//foreach ( var each in liveObjects ) {

					//	if ( string.IsNullOrEmpty( each.Item1 ) || string.IsNullOrEmpty( each.Item2 ) ) { continue; }

					//	var color = "black";
					//	if ( each.Item2 == "Mono Static Reference" ) {

					//		color = "red";
					//	}

					//	var from = gdfGenerator.AddNode( each.Item1, "white", "None" );
					//	var to = gdfGenerator.AddNode( each.Item2, "white", "None" );

					//	gdfGenerator.AddEdge( to, from, color, string.Empty );
					//}

					Console.WriteLine( "Total live objects size (MB): " + monoHeapState.GetTotalLiveObjectsSizeMb() );

					foreach ( var each in monoHeapState.GetObjectReferencesWithSize() ) {

						var sizeMb = ((float)each.Item3) / ( 1024 * 1024 );
						if ( sizeMb < 0.1f ) {

							continue;
						}

						//if ( string.IsNullOrEmpty( each.Item1 ) || string.IsNullOrEmpty( each.Item2 ) ) { continue; }

						//var color = "black";
						//if ( each.Item2 == "Mono Static Reference" ) {

						//	color = "red";
						//}

						var monoClass = gdfGenerator.AddNode( each.Item1, "white", "None" );
						var monoMethod = gdfGenerator.AddNode( each.Item2, "white", "None" );

						gdfGenerator.AddEdge( monoClass, monoMethod, "black", sizeMb.ToString().Replace(",", ".") );
					}

					gdfGenerator.Write( streamWriter );

					//foreach ( var each in liveObjects ) {

					//	//writer.WriteComment( each.GetType().Name );
					//	serializer.Serialize( writer, each );
					//	writer.WriteRaw( "\n" );
					//}

					streamWriter.Close();
				}
			}
		}
	}
}
