/*
 * BlingLib PolyMerge
 * Copyright Jelmer Cnossen 2016
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;

namespace geom
{
	/// <summary>
	/// Converts a set of polygons into non-intersecting concave polygons without loops
	/// </summary>
	public class PolyMerge
	{
		class Edge
		{
			public Poly poly;
			public Edge opposite, next, prev;
			public Vertex a, b;

			internal void InsertNext(Edge e)
			{
				// let this edge be followed by e

				e.next = next;
				if (next!=null) next.prev = e;

				e.prev = this;
				next = e;

			}
		}

		class Vertex
		{
			public Vector2 pos;
		}

		class Poly
		{
			public Edge first;

			public Vertex[] Vertices
			{
				get
				{
					Edge e = first;
					List<Vertex> vrt = new List<Vertex>();
					do {
						vrt.Add(e.a);
						e = e.next;
					} while (e != first);
					return vrt.ToArray();
				}
			}

			public Edge[] Edges
			{
				get
				{
					Edge e = first;
					List<Edge> edges = new List<Edge>();
					do {
						edges.Add(e);
						e = e.next;
					} while (e != first);
					return edges.ToArray();
				}
			}
		}

		HashSet<Vertex> vertices = new HashSet<Vertex>();
		HashSet<Poly> polygons = new HashSet<Poly>();

		public PolyMerge(Polygon[] polygons)
		{
			foreach (var pl in polygons)
			{
				var npl = new Poly();
				this.polygons.Add(npl);
				var vrt = Array.ConvertAll(pl.verts, v => new Vertex() { pos = v });
				Edge prev = null;
				for (int v1 = 0; v1< pl.verts.Length; v1++)
				{
					int v2 = (v1 + 1) % pl.verts.Length;
					Edge e = new Edge() { a = vrt[v1], b = vrt[v2], poly = npl };
					if (prev == null) { npl.first = e; e.next = e; e.prev = e;  }
					else prev.InsertNext(e);
					vertices.Add(vrt[v1]);
					prev = e;
				}

				Debug.WriteLine("Poly with {0} vertices", npl.Vertices.Length);
			}

			RemoveRedundantVertices();
			InsertVertices();
			MatchOppositeEdges();
		}

		public void PrintDist(Vector2 pos)
		{
			var edges = GetEdgeList();
			foreach (Edge e in edges)
			{
				Debug.Assert(e.next.prev == e);
				Debug.Assert(e.prev.next == e);
			}

			foreach (Poly p in polygons)
			{
				int i = 0;
				Edge e = p.first;
				do {
					e = e.next;
					var edge = new global::geom.Edge() { a = e.a.pos, b = e.b.pos };
					Debug.WriteLine("\tdist[{0}]= {1}. Edge len: {2}", i++,edge.Plane.Distance(pos),(e.a.pos-e.b.pos).Length);
				} while (e != p.first);
			}
		}

		void RemoveRedundantVertices()
		{
			Dictionary<Vertex, Vertex> oldToNew=new Dictionary<Vertex,Vertex>();
			float MinDist = 0.001f;
			List<Vertex> newVertices = new List<Vertex>();
			foreach (Vertex v in vertices)
			{
				Vertex match = null;
				foreach (Vertex b in newVertices)
				{
					if ((v.pos - b.pos).SqLength < MinDist * MinDist)
					{
						match = b;
						break;
					}
				}
				if (match != null)
					oldToNew[v] = match;
				else
				{
					newVertices.Add(v);
					oldToNew[v] = v;
				}
			}
			Debug.WriteLine("Removing {0}/{1} vertices", vertices.Count - newVertices.Count, vertices.Count);
			vertices.Clear();
			newVertices.ForEach(v => vertices.Add(v));
			foreach (Edge e in GetEdgeList())
			{
				e.a = oldToNew[e.a];
				e.b = oldToNew[e.b];
			}
		}

		void MatchOppositeEdges()
		{
			List<Edge> edges = GetEdgeList();

			// Map vertex to the edges that use that vertex as A
			Dictionary<Vertex, List<Edge>> edgeAVertex = new Dictionary<Vertex, List<Edge>>();
			foreach (Edge e in edges)
			{
				if (edgeAVertex.ContainsKey(e.a))
					edgeAVertex[e.a].Add(e);
				else
				{
					var l = new List<Edge>();
					edgeAVertex[e.a] = l;
					l.Add(e);
				}
			}

			// Map vertex to the edges that use that vertex as B
			Dictionary<Vertex, List<Edge>> edgeBVertex = new Dictionary<Vertex, List<Edge>>();

			// Match opposite edges: a == other b && b == other a
			foreach (Edge e in edges)
			{
				var candidates = edgeAVertex[e.b]; // get all where b==other a
				var found = candidates.FirstOrDefault(other => other.b == e.a);				// now find the ones that have a==other b
				if (found != null)
				{
					e.opposite = found;
					found.opposite = e;
				}
			}
		}



		void InsertVertices()
		{
			const float MinDist=0.001f;
			List<Edge> edges = GetEdgeList();
			foreach (var v in vertices)
			{
				for (int i = 0; i < edges.Count; i++)
				{
					Edge e = edges[i];
					// find any vertices that are not part of this edge and are still on it
					// if one is found, split the edge. Add the 2 newly generated edges to the end
					if (v == e.a || v == e.b) continue;

					float dist=v.pos.DistanceToLine(e.a.pos, e.b.pos);
					if (dist < MinDist && (e.a.pos-v.pos).SqLength>MinDist*MinDist && (e.b.pos-v.pos).SqLength>MinDist*MinDist)
					{
						Edge nedge = new Edge() { a = v, b = e.b, next = e.next, prev = e, poly = e.poly };
						e.next.prev = nedge;
						e.next = nedge;
						e.b = v;
						edges.Add(nedge);
						//						Debug.WriteLine("Added edge");
					}
				}
			}
		}

		private List<Edge> GetEdgeList()
		{
			List<Edge> edges = new List<Edge>();
			foreach (var pl in polygons)
			{
				Edge e = pl.first;
				do
				{
					edges.Add(e);
					e = e.next;
				} while (e != pl.first);
			}
			return edges;
		}

		public Polygon[] GetPolygons()
		{
			List<Polygon> rpl = new List<Polygon>();
			foreach (Poly pl in polygons)
				rpl.Add(new Polygon(Array.ConvertAll(pl.Vertices, v => v.pos)));
			return rpl.ToArray();
		}

		public void MergePolygons()
		{
			List<Edge> edges = GetEdgeList();

			foreach (Edge e in edges)
			{
				if (e.opposite == null)
					continue;

				if (e.poly == e.opposite.poly)
					continue;

				MergePoly(e);
			}

			HashSet<Edge> edgesWithOpposites = new HashSet<Edge>(GetEdgeList().Where(e => e.opposite != null));
			bool found;
			do
			{
				found = false;

				foreach (Edge e in edgesWithOpposites)
				{
					if (e.next == e.opposite)
					{
						e.prev.next = e.opposite.next;
						e.opposite.next.prev = e.prev;
						e.poly.first = e.prev;
						found = true;
						edgesWithOpposites.Remove(e);
						edgesWithOpposites.Remove(e.opposite);
						break;
					}
				}
			} while (found);
		}

		public void RemoveOneEdge()
		{
			List<Edge> edges = GetEdgeList();

			foreach (Edge e in edges)
			{
				if (e.opposite == null)
					continue;

				if (e.poly == e.opposite.poly)
					continue;

				MergePoly(e);
				break;
			}
		}

		void MergePoly(Edge e)
		{
			Debug.Assert(e.opposite != null);

			// The 2 oposites are going in opposite direction, so connect other's previous to next, and other's next to previous
			Edge op = e.opposite;
			Poly opPoly = op.poly;
			Edge[] opEdges = opPoly.Edges;

			// Next to other previous
			op.prev.next = e.next;
			e.next.prev = op.prev;

			// Previous to other next
			op.next.prev = e.prev;
			e.prev.next = op.next;

			// Modify parent polygon
			foreach (var a in opEdges) a.poly = e.poly;

			op.opposite = null;
			e.opposite = null;
			e.poly.first = e.next;

			polygons.Remove(opPoly);
		}

	}
}
