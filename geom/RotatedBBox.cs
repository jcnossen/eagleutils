using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace geom
{
	public struct RotatedBBox
	{
		Vector2 size;
		XForm xform;
		Vector2[] corners;

		public XForm XForm
		{
			get { return xform; }
		}

		public float Width
		{
			get { return size.x; }
		}

		public float Height
		{
			get { return size.y; }
		}

		public Vector2[] Corners
		{
			get {
				if(corners==null)
					corners = GetCorners();
				return corners;
			}
		}

		public RotatedBBox(Vector2 size, XForm xf)
		{
			this.size = size;
			this.xform = xf;
			corners = null;
		}

		public Box2 GetAABBox()
		{
			var c = Corners;
			var box = new Box2(c[0], c[0]);
			for (int i = 1; i < 4; i++)
				box.Extend(c[i]);
			return box;
		}

		Vector2[] GetCorners()
		{
			var hs = size * 0.5f;
			var v = new Vector2[]
			{
				-hs, new Vector2(hs.x,-hs.y),
				hs, new Vector2(-hs.x,hs.y)
			};
			for (int i = 0; i < 4; i++)
				v[i] = xform.Transform(v[i]);
			return v;
		}

		public bool IsPointInside(Vector2 pt)
		{
			Vector2[] c = Corners;
			bool outside = false;
			for (int i = 0; i < 4; i++)
			{
				Vector2 a = c[i];
				Vector2 b = c[(i + 1) % 4];
				Vector2 dir = (a - b).Ortho;
				float da = dir.Dot(a);
				float dist = dir.Dot(pt) - da;
				if (dir.Dot(pt) > da)
					outside = true;
			}
			return !outside;
		}

		public float DistanceToPoint(Vector2 pt)
		{
			return ClosestPointTo(pt).Length;
		}

		public Vector2 ClosestPointTo(Vector2 pt)
		{
			Vector2[] c = Corners;
			bool outside = false;
			float minSqDist = float.MaxValue;
			Vector2 closestPos = c[0];
			for (int i = 0; i < 4; i++)
			{
				Vector2 a = c[i];
				Vector2 b = c[(i + 1) % 4];
				Vector2 closest = pt.ClosestPosOnLine(a, b);
				float sqDist = (closest - pt).SqLength;
				if (sqDist < minSqDist)
				{
					closestPos = closest;
					minSqDist = sqDist;
				}

				Vector2 dir = (a - b).Ortho;
				float da = dir.Dot(a);
				if (dir.Dot(pt) > da)
					outside = true;
			}
			if (!outside) return pt;
			return closestPos;

		}

	}
}

