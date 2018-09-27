using System;
using System.Collections.Generic;
using System.IO;

namespace HeapParser
{
	public class GdfGenerator
	{
		public class Node {
			public string name;
			public string color;
			public string description;
		}

		private List<Node> _nodes = new List<Node>();
		private List<Tuple<int, int, string, string>> _edges = new List<Tuple<int, int, string, string>>();

		public GdfGenerator()
		{
		}

		public int AddNode(string name, string color, string description) {

			_nodes.Add( new Node { name = name.Replace(",", "$"), color = color, description = description } );
			return _nodes.Count - 1;
		}

		public void AddEdge(int fromNode, int toNode, string color, string description) {

			_edges.Add( new Tuple<int, int, string, string>( fromNode, toNode, color, description ) );
		}

		public void Write( TextWriter outputStream ) {

			WriteNodeHeader( outputStream );

			for (var i = 0; i < _nodes.Count; ++i) {

				WriteNode( outputStream, i );
			}

			WriteEdgeHeader( outputStream );

			foreach(var each in _edges) {

				WriteEdge( outputStream, each.Item1, each.Item2, each.Item3, each.Item4 );
			}
		}

		private void WriteNodeHeader( TextWriter writer ) {

			writer.WriteLine( "nodedef> name,color,label" );
		}

		private void WriteNode(TextWriter writer, int nodeId) {

			var node = _nodes[nodeId];
			var nodeString = string.Format( "{0},{1},{2}", node.name, node.color, node.name );

			writer.WriteLine( nodeString );
		}

		private void WriteEdgeHeader(TextWriter writer ) {

			writer.WriteLine( "edgedef> node1,node2,color,directed,label" );
		}

		private void WriteEdge( TextWriter writer, int fromNodeId, int toNodeId, string color, string description ) {

			writer.WriteLine( string.Format( "{0},{1},{2},true,{3}", _nodes[fromNodeId].name, _nodes[toNodeId].name, color, description ) );
		}
	}
}
