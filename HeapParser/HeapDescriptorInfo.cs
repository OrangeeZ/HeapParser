using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HeapParser
{
    public class HeapDescriptorInfo
    {
        public readonly Dictionary<System.Type, int> Sizes = new Dictionary<System.Type, int>();

        public void Initialize()
        {
            foreach (var each in GetHeapDescriptorTypes())
            {
                Sizes[each] = GetTypeSize(each);

                Console.WriteLine($"{each}:{Sizes[each]}");
            }
        }

        private static IEnumerable<System.Type> GetHeapDescriptorTypes()
        {
            var types = Assembly.GetCallingAssembly().DefinedTypes.Where(_ => _.IsSubclassOf(typeof(HeapDescriptor)));
            return types;
        }

        private static int GetTypeSize(System.Type type)
        {
            var fields = type.GetFields();
            var result = 0;

            foreach (var each in fields)
            {
                var fieldType = each.FieldType;
                if (fieldType.IsValueType)
                {
                    result += Marshal.SizeOf(fieldType);
                }
                else
                {
                    result = -1;
                    break;
                }
            }

            return result;
        }
    }

    public class HeapDescriptorFactory
    {
        public Type[] MatchingTypes;
        private readonly Func<HeapDescriptor>[] _constructors;

        public HeapDescriptorFactory(HeapDescriptorInfo heapDescriptorInfo)
        {
            var tags = Enum.GetValues(typeof(HeapTag)).OfType<HeapTag>().ToArray();
            var tagCount = Enum.GetValues(typeof(HeapTag)).OfType<HeapTag>().Max(_ => (int) _);

            Console.WriteLine($"HeapTag count: {tagCount}");

            _constructors = new Func<HeapDescriptor>[tagCount];

            MatchingTypes = new Type[tagCount];
            
            foreach (var each in heapDescriptorInfo.Sizes.Keys)
            {
                var matchingTag = tags.FirstOrDefault(_ =>
                    string.Compare(_.ToString(), each.Name, StringComparison.InvariantCultureIgnoreCase) == 0);

                if (matchingTag != default(HeapTag))
                {
                    var constructorExpression = Expression.New(each);
                    var expressionLambda = Expression.Lambda<Func<HeapDescriptor>>(constructorExpression);

                    RegisterTagAndConstructor(matchingTag, expressionLambda.Compile());
                    
                    Console.WriteLine($"Generated constructor for {each} : {expressionLambda}");

                    MatchingTypes[(int) matchingTag] = each;
                }
                else
                {
                    Console.WriteLine($"Could not generate constructor for {each}");
                }
            }
            
        }

        public void RegisterTagAndConstructor(HeapTag tag, Func<HeapDescriptor> constructor)
        {
            _constructors[(int) tag] = constructor;
        }

        public HeapDescriptor GetInstance(HeapTag tag)
        {
            return _constructors[(int) tag]();
        }
    }
}