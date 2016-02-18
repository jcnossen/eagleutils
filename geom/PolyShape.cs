/*
 * geom PolyShape
 * Copyright Jelmer Cnossen 2016
 */
using geom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;


namespace geom
{
	/// <summary>
	/// A shape of polygons, without shading info
	/// This is used to represent by PolygonItem to support concave polygons
	/// 
	/// Features:
	/// 
	/// CSG operations (add/subtract polygon from shape)
	/// Shape from concave line loop
	/// 
	/// Implementation:
	/// 
	/// Node based BSP
	/// </summary>
	public class PolyShape
	{
		public TreeNode root;
		public Box2 bbox;

		public class TreeNode 
		{
			public Edge edge;
			public Plane2 plane;
			public TreeNode[] childs = new TreeNode[2];
			public TreeNode parent;

			public TreeNode Clone()
			{
				TreeNode c = new TreeNode()
				{
					edge = edge,
					plane = plane
				};

				for (int i = 0; i < 2; i++)
					if (childs[i] != null) {
						c.childs[i] = childs[i].Clone();
						c.childs[i].parent=c;
					}
				return c;
			}

			public void Print(TextWriter w, int indent)
			{
				w.Write(new string(' ', indent) + "Front: ");
				if (childs[0] != null)
				{
					w.WriteLine();
					childs[0].Print(w, indent + 1);
				}
				else w.WriteLine(" null");
				w.Write(new string(' ', indent) + "Back: ");
				if (childs[1] != null)
				{
					w.WriteLine();
					childs[1].Print(w, indent + 1);
				}
				else w.WriteLine(" null");
			}

			/// <summary>
			/// Checks if the point p is inside the model using the BSP
			/// </summary>
			public bool PointInside(Vector2 p)
			{
				float d = plane.Distance(p);
				if (d > 0) {
					if (childs[0] != null)
						return childs[0].PointInside(p);
					return false;
				}
				else {
					if (childs[1] != null)
						return childs[1].PointInside(p);
					return true;
				}
			}

			/// <summary>
			/// Recursively divide the polygon up and return true if any piece ends up on the tree front side
			/// </summary>
			/// <param name="polygon"></param>
			/// <returns></returns>
			public bool IsInside(Polygon polygon)
			{
				Polygon[] splits = polygon.Split(plane);

				bool f = false, b = false;
				if (splits[0] != null) 
					f = (childs[0] != null) ? childs[0].IsInside(splits[0]) : false;
				if (splits[1] != null)
					b = (childs[1] != null) ? childs[1].IsInside(splits[1]) : true;
				return f || b;
			}

			// Side indicates the side that has to be deleted
			public Polygon[] ClipPolygon(Polygon pl, PlaneSide sideToDelete)
			{
				PlaneSide r = pl.CheckAgainstPlane(plane);
				Polygon[] nullList = new Polygon[] { };

				if (r == PlaneSide.On) {
					// In the 2D case, PlaneSide.On means a degenerate polygon, so it can be ignored
					return nullList;
				}

				if (r == PlaneSide.Both) {
					Polygon fr, bk;
					Polygon[] fr_ret, bk_ret;
					pl.Split(plane, out fr, out bk);

					// Check front
					if (childs[0] != null)
						fr_ret = childs[0].ClipPolygon(fr, sideToDelete);
					else {
						if (sideToDelete == PlaneSide.Front)
							fr_ret = nullList;
						else
							fr_ret = new Polygon[] { fr };
					}

					// Check back
					if (childs[1] != null)
						bk_ret = childs[1].ClipPolygon(bk, sideToDelete);
					else {
						if (sideToDelete == PlaneSide.Back) {
							bk_ret = nullList;
						}
						else bk_ret = new Polygon[] { bk };
					}

					// If the clipped parts remained unmodified, then the original polygon can be returned
					if (fr_ret.Length == 1 && fr_ret[0] == fr && bk_ret.Length == 1 && bk_ret[0] == bk) {
						return new Polygon[] { pl };
					}

					List<Polygon> result = new List<Polygon>();
					result.AddRange(fr_ret);
					result.AddRange(bk_ret);
					return result.ToArray();
				}
				else if (r == PlaneSide.Front) {
					if (childs[0] != null)
						return childs[0].ClipPolygon(pl, sideToDelete);
					if (sideToDelete != PlaneSide.Front)
						return new Polygon[] { pl };
				}
				else if (r == PlaneSide.Back) {
					if (childs[1] != null)
						return childs[1].ClipPolygon(pl, sideToDelete);
					if (sideToDelete != PlaneSide.Back)
						return new Polygon[] { pl };
				}

				return nullList;
			}


		}

		public PolyShape()
		{	}

		public PolyShape(IEnumerable<Edge> edges)
		{
			InitFromEdgeList(edges);
		}

		public PolyShape(Polygon[] polygons)
		{
//			this.polygons = polygons;
	//		if (polygons.Length == 0)
		//		return;
			List<Edge> edgesList=new List<Edge>();
			foreach (var pl in polygons) 
				edgesList.AddRange(Edge.EdgesFromPoly(pl));

			InitFromEdgeList(edgesList);
		}

		void InitFromEdgeList(IEnumerable<Edge> edgeList)
		{
			root = BuildTree_BestSplitter(edgeList.ToArray());

			bbox.min = bbox.max = edgeList.First().a;
			foreach (Edge e in edgeList)
			{
				bbox.Extend(e.a);
				bbox.Extend(e.b);
			}
		}

		private TreeNode BuildTree_BestSplitter(Edge[] list)
		{
			List<Edge> frontlist = new List<Edge>(list.Length);
			List<Edge> backlist = new List<Edge>(list.Length);

			TreeNode tn = new TreeNode();
			tn.edge = FindBestSplitter(list);
			tn.plane = tn.edge.Plane;

			foreach (Edge e in list) {
				if (e == tn.edge) // dont split polygon against itself
					continue;

				PlaneSide r = e.CheckAgainstPlane(tn.plane);
				if (r == PlaneSide.On) {
					if (tn.plane.norm.Dot(e.Plane.norm) > 0)
						// add to front
						frontlist.Add(e);
					else
						// add to back
						backlist.Add(e);
				}
				else if (r == PlaneSide.Front)
					frontlist.Add(e);
				else if (r == PlaneSide.Back)
					backlist.Add(e);
				else if (r == PlaneSide.Both) {
					// add to both
					frontlist.Add(e);
					backlist.Add(e);
				}
			}

			if (frontlist.Count > 0) {
				tn.childs[0] = BuildTree_BestSplitter(frontlist.ToArray());
			}

			if (backlist.Count() > 0) {
				tn.childs[1] = BuildTree_BestSplitter(backlist.ToArray());
			}
			return tn;
		}

		public Polygon[] ToPolygons()
		{
			Box2 bbox = BBox;
			var quadPoly = Polygon.Quad(bbox);
			return ClipPolygon(quadPoly, PlaneSide.Front);
		}

		static Edge FindBestSplitter(Edge[] list)
		{
			Edge best = null;
			int best_score = 0;

			for (int a = 0; a < list.Length; a++) {
				int backs, fronts, splits;
				backs = fronts = splits = 0;

				for (int b = 0; b < list.Length; b++) {
					if (b == a) continue;

					PlaneSide result = list[b].CheckAgainstPlane(list[a].Plane);
					if (result == PlaneSide.Front) fronts++;
					else if (result == PlaneSide.Back) backs++;
					else if (result == PlaneSide.Both) {
						fronts++; backs++; splits++;
					}
				}

				int score = Math.Abs(backs - fronts) + splits * 3;

				if (best == null || score < best_score) {
					best_score = score;
					best = list[a];
				}
			}

			return best;
		}

		public void AddEdges(Edge[] edges)
		{
			if (root == null)
				root = BuildTree_BestSplitter(edges); // slightly unoptimal but whatever
			else
			{
				InsertEdges(root, edges);
			}

			foreach (Edge e in edges)
			{
				bbox.Extend(e.a);
				bbox.Extend(e.b);
			}
		}

		/// <summary>
		/// Insert edges: Keep edges on front, delete on back
		/// </summary>
		/// <param name="node"></param>
		/// <param name="edges"></param>
		void InsertEdges(TreeNode node, Edge[] edges)
		{
			List<Edge> frontList=new List<Edge>();
			List<Edge> backList=new List<Edge>();
			foreach (Edge e in edges)
			{
				PlaneSide r = e.CheckAgainstPlane(node.plane);
				if (r == PlaneSide.Front)
					frontList.Add(e);
				else if (r == PlaneSide.Back)
					backList.Add(e);
				else if (r == PlaneSide.Both || r==PlaneSide.On)
				{
					// add to both
					frontList.Add(e);
					backList.Add(e);
				}
			}

			if (frontList.Count > 0)
			{
				if (node.childs[0] == null)
					node.childs[0] = BuildTree_BestSplitter(frontList.ToArray());
				else
					InsertEdges(node.childs[0], frontList.ToArray());
			}			
			if (backList.Count > 0)
			{
				if (node.childs[1] == null)
					return;
//					node.childs[1] = BuildTree_BestSplitter(backList.ToArray());
				else
					InsertEdges(node.childs[1], backList.ToArray());
			}
		}

		

		public bool PointInside(Vector2 pos)
		{
			if (root == null)
				return false;
			return root.PointInside(pos);
		}

		public bool IsTouching(Polygon polygon)
		{
			return root.IsInside(polygon);
		}


		public PolyShape Clone()
		{
			PolyShape shape = new PolyShape();
			shape.root = root.Clone();
			shape.bbox = bbox; 
			return shape;
		}

		public Box2 BBox
		{
			get {
				return bbox;
			}
		}

		public void AddOverlappingPolygons(Polygon[] plList, bool additive=true)
		{
			if (plList.Length == 0)
				return;

			AddEdges(Edge.EdgesFromPoly(plList[0]));
			for (int i=1;i<plList.Length;i++)
			{
				var pl = plList[i];
				var clipped = ClipPolygon(pl, additive? PlaneSide.Back : PlaneSide.Front);
				AddPolygons(clipped);
			}
		}

		public void AddPolygons(Polygon[] plList)
		{
			List<Edge> edges = new List<Edge>();

			foreach (Polygon pl in plList)
				edges.AddRange(Edge.EdgesFromPoly(pl));

			AddEdges(edges.ToArray());
		}

		public bool PolygonsOutside(Polygon[] list)
		{
			foreach (Polygon pl in list)
				if (PolygonInside(pl))
					return false;
			return true;
		}

		/// <summary>
		/// Split up the polygon and see if any of the parts end up inside
		/// </summary>
		public bool PolygonInside(Polygon pl)
		{
			return ClipPolygon(pl, PlaneSide.Front).Length > 0;
		}

		public Polygon[] ClipPolygon(Polygon plbb, PlaneSide removeSide)
		{
			if (root == null)
			{
				if (removeSide == PlaneSide.Back) // dont remove anything
					return new Polygon[] { plbb };
				else if (removeSide == PlaneSide.Front)
					return new Polygon[] { };
				else throw new ArgumentException("removeSide should be front/back");
			}
			else
				return root.ClipPolygon(plbb, removeSide);
		}

		public void Print(TextWriter w)
		{
			if (root == null)
			{
				w.WriteLine("Tree: null");
			}
			else root.Print(w, 0);
		}
	}

	public class Polygon
	{
		public Vector2[] verts;

		public const float Epsilon = 0.001f;

		public Vector2 this[int i]
		{
			get { return verts[i]; }
			set { verts[i] = value; }
		}

		[XmlIgnore]
		public int Length
		{
			get { return verts.Length; }
		}

		public Polygon(Vector2[] vpos)
		{
			verts = new Vector2[vpos.Length];
			for (int i = 0; i < vpos.Length; i++)
			{
				verts[i] = vpos[i];
			}
		}

		public Polygon(int n)
		{
			verts = new Vector2[n];
		}

		[XmlIgnore]
		public Vector2 Center
		{
			get
			{
				Vector2 a = new Vector2();
				for (int i = 0; i < verts.Length; i++)
					a += verts[i];
				return a / verts.Length;
			}
			set
			{
				Vector2 move = value - Center;
				for (int i = 0; i < verts.Length; i++)
					verts[i] += move;
			}
		}


		public Vector2[] FlipVertices()
		{
			Vector2[] v = new Vector2[verts.Length];

			for (int x = 0; x < verts.Length; x++)
				v[x] = verts[verts.Length - x - 1];

			return v;
		}

		public void Flip()
		{
			verts = FlipVertices();
		}


		public Polygon Clip(Plane2 clipPlane)
		{
			Vector2[] cvrt = ClipVerticesToPlane(clipPlane);
			if (cvrt.Length > 0)
			{
				return new Polygon(cvrt);
			}

			return null;
		}

		public Vector2[] ClipVerticesToPlane(Plane2 pl)
		{
			List<Vector2> dst = new List<Vector2>(verts.Length);

			int v1 = verts.Length - 1;
			for (int v2 = 0; v2 < verts.Length; v1 = v2++)
			{
				float d1 = pl.Distance(verts[v1]);
				float d2 = pl.Distance(verts[v2]);

				if (d1 >= 0 && d2 >= 0)  // Both on front, add end point(v2)
					dst.Add(verts[v2]);

				// Crossing plane
				if ((d1 < 0 && d2 >= 0) || (d1 >= 0 && d2 < 0))
				{
					float part = d1 / (d1 - d2);
					Vector2 interp = Vector2.Interpolate(verts[v1], verts[v2], part);
					dst.Add(interp);
					if (d1 < 0)
						dst.Add(verts[v2]);
				}
			}

			return dst.ToArray();
		}


		public void SplitVertices(Plane2 pl, out Vector2[] frontVrt, out Vector2[] backVrt)
		{
			List<Vector2> fv = new List<Vector2>();
			List<Vector2> bv = new List<Vector2>();

			frontVrt = backVrt = null;

			int v1 = verts.Length - 1;
			for (int v2 = 0; v2 < verts.Length; v1 = v2++)
			{
				float d1 = pl.Distance(verts[v1]);
				float d2 = pl.Distance(verts[v2]);

				if (d1 >= 0 && d2 >= 0)  // Both on front, add end point(v2)
					fv.Add(verts[v2]);

				else if (d1 < 0 && d2 < 0) // Both on back, add end point (v2)
					bv.Add(verts[v2]);

				else if ((d1 < 0 && d2 >= 0) || (d1 >= 0 && d2 < 0))
				{
					float part = d1 / (d1 - d2);
					Vector2 interp = Vector2.Interpolate(verts[v1], verts[v2], part);

					fv.Add(interp);
					bv.Add(interp);

					if (d1 < 0)
						fv.Add(verts[v2]);
					else
						bv.Add(verts[v2]);
				}
			}

			if (fv.Count > 0)
				frontVrt = fv.ToArray();
			if (bv.Count > 0)
				backVrt = bv.ToArray();
		}

		public Polygon[] Split(Plane2 plane)
		{
			Vector2[] front, back;
			SplitVertices(plane, out front, out back);

			Polygon[] result = new Polygon[2];
			if (front != null)
				result[0] = new Polygon(front);
			if (back != null)
				result[1] = new Polygon(back);
			return result;
		}

		public void Split(Plane2 plane, out Polygon fr, out Polygon bk)
		{
			Vector2[] front, back;
			SplitVertices(plane, out front, out back);

			fr = (front != null) ? new Polygon(front) : null;
			bk = (back != null) ? new Polygon(back) : null;
		}


		public PlaneSide CheckAgainstPlane(Plane2 pln)
		{
			bool front = true;
			bool back = true;
			bool on = true;

			foreach (Vector2 v in verts)
			{
				float dis = pln.Distance(v);
				if (dis < -Epsilon) front = on = false;
				if (dis > Epsilon) back = on = false;
			}

			if (on) return PlaneSide.On;
			if (front) return PlaneSide.Front;
			if (back) return PlaneSide.Back;
			return PlaneSide.Both;
		}
		/// <summary>
		/// Point Inside meaning: All the planes have the point on their backside
		/// </summary>
		/// <param name="pos"></param>
		/// <returns></returns>
		public bool PointInside(Vector2 pos)
		{
			for (int i = 0; i < verts.Length; i++)
			{
				Vector2 edge = verts[(i + 1) % verts.Length] - verts[i];
				Vector2 edgeNormal = edge.Ortho;

				if (edgeNormal.Dot(pos) > edgeNormal.Dot(verts[i]))
					return false;
			}
			return true;
		}

		[XmlIgnore]
		public Box2 BBox
		{
			get
			{
				Box2 box;
				box.min = box.max = verts[0];
				box.Extend(this);
				return box;
			}
		}

		public static Polygon Quad(Box2 box)
		{
			return Quad(box.min, box.max);
		}

		public static Polygon Quad(Vector2 a, Vector2 b)
		{
			Polygon pl = new Polygon(4);

			pl[0] = new Vector2(a.x, a.y);
			pl[1] = new Vector2(a.x, b.y);
			pl[2] = new Vector2(b.x, b.y);
			pl[3] = new Vector2(b.x, a.y);

			return pl;
		}

		public Edge GetEdge(int i)
		{
			return new Edge() { a = verts[i], b = verts[(i + 1) % Length], owner = this };
		}

		public bool IsTouching(Polygon pl)
		{
			// See if any of the vertices are inside the other poly, or vice versa
			for (int i = 0; i < pl.Length; i++)
			{
				if (PointInside(pl[i]))
					return true;
			}
			for (int i = 0; i < Length; i++)
			{
				if (pl.PointInside(verts[i]))
					return true;
			}
			return false;
		}


		public Polygon Clone()
		{
			return new Polygon(
				verts = (Vector2[])verts.Clone()
			);
		}

		public Polygon Transform(XForm xform)
		{
			return new Polygon(Array.ConvertAll(verts,
				v => xform.Transform(v)
				));
		}

		public void _Rasterize(Action<Vector2> drawPoint, float gridX, float gridY)
		{
			Int2[] grv = Array.ConvertAll(verts, v => new Int2((int)(v.x / gridX), (int)(v.y / gridY)));

			int miny = grv[0].y, maxy = grv[0].y;
			int maxx = grv[0].x, minx = grv[0].x;
			for(int i=0;i<verts.Length;i++)
			{
				if (miny > grv[i].y) miny = grv[i].y;
				if (maxy < grv[i].y) maxy = grv[i].y;
				if (maxx < grv[i].x) maxx = grv[i].x;
				if (minx > grv[i].x) minx = grv[i].x;
			}
			int h = maxy - miny+1;
			int[] left = new int[h];
			int[] right = new int[h];

			for (int i = 0; i < h; i++)
			{
				left[i] = maxx;
				right[i] = miny;
			}

			// go through the edges and draw them into left/right
			for (int i = 0; i < verts.Length - 1; i++)
			{
				int v1 = i, v2 = (i + 1) % verts.Length;
				Int2 a, b;

				if (grv[v1].y == grv[v2].y)
					continue;
	
				if (grv[v1].y > grv[v2].y)
				{ a = grv[v2]; b = grv[v1]; }
				else
				{ a = grv[v1]; b = grv[v2]; }

				float deltaX = (b.x - a.x) / (float)(b.y - a.y);
				float xpos = a.x;
				for (int y = a.y; y <= b.y; y++)
				{
					if (left[y - miny] > xpos) left[y - miny] = (int)xpos;
					if (right[y - miny] < xpos) right[y - miny] = (int)xpos;
					xpos += deltaX;
				}
			}

			for (int y = miny; y <= maxy; y++)
				for (int x = left[y - miny]; x <= right[y - miny]; x++)
					drawPoint(new Vector2(x * gridX, y * gridY));
		}

		public void Rasterize(float gridX, float gridY, Action<Vector2> drawPoint)
		{
			//todo: make some decent rasterization
			Vector2 minPos, maxPos;
			minPos = maxPos = verts[0];

			foreach (var v in verts)
			{
				minPos.Min(v);
				maxPos.Max(v);
			}

			Vector2 grid=new Vector2(gridX,gridY);
			minPos = new Vector2((int)(minPos.x / gridX), (int)(minPos.y / gridY));
			maxPos = new Vector2((int)(maxPos.x / gridX), (int)(maxPos.y / gridY));

			minPos *= grid;
			maxPos *= grid;

			for(float y=minPos.y-1;y<=maxPos.y+1;y+=gridY)
				for (float x=minPos.x-1; x <=maxPos.x+1; x+=gridX)
				{
					if (PointInside(new Vector2(x - 0.5f, y - 0.5f)) || PointInside(new Vector2(x - 0.5f, y + 0.5f)) ||
						PointInside(new Vector2(x + 0.5f, y + 0.5f)) || PointInside(new Vector2(x + 0.5f, y - 0.5f)))
					{
						drawPoint(new Vector2(x,y));
					}
				}
		}

		public Box2.Side CompareWithBox(Box2 b)
		{
			return b.Compare(BBox);
		}

		public Vector2 ClosestPoint(Vector2 pt)
		{
			int closestEdge = 0;
			float bestDist = float.MaxValue;
			for (int i = 0; i < verts.Length; i++)
			{
				var a = verts[i];
				var b = verts[(i + 1) % verts.Length];
				float dist = pt.DistanceToLine(a, b);
				if(dist<bestDist)
				{
					bestDist = dist;
					closestEdge = i;
				}
			}
			return pt.ClosestPosOnLine(verts[closestEdge], verts[(closestEdge + 1) % verts.Length]);
		}
	}


	public class Edge
	{
		public const float Epsilon = 0.001f;

		public Vector2 a, b;
		public Polygon owner; // can be null

		[XmlIgnore]
		public Plane2 Plane
		{
			get { return Plane2.FromEdge(a, b); }
		}

		[XmlIgnore]
		public Vector2 Middle
		{
			get { return (a + b) * 0.5f; }
		}
		[XmlIgnore]
		public Vector2 Ortho
		{
			get { return (b - a).Ortho.Normalized; }
		}

		public PlaneSide CheckAgainstPlane(Plane2 pln)
		{
			bool front = true;
			bool back = true;
			bool on = true;

			float d1 = pln.Distance(a);
			float d2 = pln.Distance(b);

			if (d1 < -Epsilon) front = on = false;
			if (d1 > Epsilon) back = on = false;
			if (d2 < -Epsilon) front = on = false;
			if (d2 > Epsilon) back = on = false;

			if (on) return PlaneSide.On;
			if (front) return PlaneSide.Front;
			if (back) return PlaneSide.Back;
			return PlaneSide.Both;
		}

		static public Edge[] EdgesFromPoly(Polygon pl)
		{
			Edge[] edges = new Edge[pl.verts.Length];
			for (int i = 0; i < pl.verts.Length; i++)
				edges[i] = new Edge()
				{
					a = pl.verts[i],
					b = pl.verts[(i + 1) % pl.verts.Length],
					owner = pl
				};
			return edges;
		}

		public bool IsPointOnEdge(Vector2 pos)
		{
			return pos.DistanceToLine(a, b) < Epsilon;
		}

		public float Length {
			get { return (a - b).Length; }
		}
	}
}


public static class PolygonHelpers
{
	public static void Extend(this Box2 box, Polygon pl)
	{
		for (int i = 0; i < pl.Length; i++)
			box.Extend(pl[i]);
	}


	public static Polygon GetPolygon(this RotatedBBox bbox)
	{
		return new Polygon(new Vector2[] {
				new Vector2(-bbox.Width/2,bbox.Height/2),
				new Vector2(bbox.Width/2,bbox.Height/2),
				new Vector2(bbox.Width/2,-bbox.Height/2),
				new Vector2(-bbox.Width/2,-bbox.Height/2)
			}).Transform(bbox.XForm);
	}
}

