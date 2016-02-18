/*
 * BlingLib Math
 * Copyright Jelmer Cnossen 2016
 */
using Newtonsoft.Json;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Serialization;


namespace geom
{
	public struct Box2
	{
		public Vector2 min, max;

		public Box2(Vector2 Min, Vector2 Max)
		{
			min = Min; max = Max;
		}
		public Box2(float xs, float ys, float xe, float ye)
		{
			min.x = xs; min.y = ys;
			max.x = xe; max.y = ye;
		}

		public Vector2[] Vertices
		{
			get
			{
				return new Vector2[] {
					min, new Vector2(min.x, max.y),
					max, new Vector2( max.x,min.y)
				};
			}
		}

		public void Extend(Vector2 v)
		{
			min.Min(v);
			max.Max(v);
		}

		public void Extend(Box2 b)
		{
			max.Max(b.max);
			min.Min(b.min);
		}

//		public void Extend(Polygon pl)
//		{
//			for (int i = 0; i < pl.Length; i++)
//				Extend(pl[i]);
//		}

		public Vector2 Size
		{
			get { return max - min; }
		}

		public float Width
		{
			get { return max.x - min.x; }
		}
		public float Height
		{
			get { return max.y - min.y; }
		}

		public Vector2 Center
		{
			get { return (max + min) * 0.5f; }
		}

		public void Extend(IEnumerable<Vector2> verts)
		{
			foreach (Vector2 v in verts)
				Extend(v);
		}

		public enum Side
		{
			Inside, Outside, Both
		}

		public Side Compare(Box2 b)
		{
			if (b.min.x >= min.x && b.min.y >= min.y &&
				b.max.x <= max.x && b.max.y <= max.y)
			{
				return Side.Inside;
			}

			if (b.max.x <= min.x || b.max.y <= min.y ||
				b.min.x >= max.x || b.min.y >= max.y)
				return Side.Outside;

			return Side.Both;
		}
	}

	public static class Vector2Extension
	{
//		public static Vector2 ClosestPosOnEdge(this Vector2 pos, Edge e)
//		{
//			return pos.ClosestPosOnLine(e.a, e.b);
//		}
	}

	public enum PlaneSide
	{
		Front,Back,On,Both
	}

	public struct Plane2
	{
		public Vector2 norm;
		public float d;

		public Plane2(Vector2 n, float d)
		{
			norm = n;
			this.d = d;
		}

		public float Distance(Vector2 x)
		{
			return x.Dot(norm) - d;
		}

		public Plane2 Flipped
		{
			get { return new Plane2(-norm, -d); }
		}

		public static Plane2 operator -(Plane2 x)
		{
			return x.Flipped;
		}

		public static Plane2 FromEdge(Vector2 a, Vector2 b)
		{
			Vector2 dir = b - a;
			Vector2 n = dir.Ortho.Normalized;
			
			return new Plane2(n, n.Dot(a));
		}

//		public static Plane2 FromEdge(Edge edge)
	//	{
		//	return FromEdge(edge.a, edge.b);
		//}
	}
	public struct Line
	{
		public Vector2 a, b;
	}

	public struct Int2: IEquatable<Int2>
	{
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is Int2 && Equals((Int2) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (x*397) ^ y;
			}
		}

		public Int2(int x, int y)
		{
			this.x = x; this.y = y;
		}

		[XmlAttribute]
		public int x, y;

		public static implicit operator Vector2(Int2 p)
		{
			return new Vector2(p.x, p.y);
		}

		public void Clamp(int w, int h)
		{
			if (x < 0) x = 0; if (x > w - 1) x = w - 1;
			if (y < 0) y = 0; if (y > h - 1) y = h - 1;
		}

		public Int2 Clamped(int w,int h)
		{
			var r = this;
			r.Clamp(w, h);
			return r;
		}

		public static Int2 operator +(Int2 a, Int2 b)
		{
			Int2 r;
			r.x = a.x + b.x;
			r.y = a.y + b.y;
			return r;
		}

		public static Int2 operator -(Int2 a, Int2 b)
		{
			Int2 r;
			r.x = a.x - b.x;
			r.y = a.y - b.y;
			return r;
		}

		public static Int2 operator -(Int2 a, int b)
		{
			Int2 r;
			r.x = a.x - b;
			r.y = a.y - b;
			return r;
		}

		public static Vector2 operator -(float a, Int2 b)
		{
			return a - new Vector2(b);
		}
		public static Vector2 operator +(float a, Int2 b)
		{
			return a + new Vector2(b);
		}
		public static Vector2 operator -(Int2 a, float b)
		{
			return new Vector2(a) - b;
		}
		public static Vector2 operator +(Int2 a, float b)
		{
			return new Vector2(a) + b;
		}
		public static Vector2 operator*(float a, Int2 b)
		{
			return new Vector2(b) * a;
		}
		public static Vector2 operator*(Int2 a, float b)
		{
			return new Vector2(a) * b;
		}
		public static Vector2 operator/(Int2 a, float b)
		{
			return new Vector2(a) / b;
		}
		public static Int2 operator /(Int2 a, int b)
		{
			return new Int2(a.x / b, a.y/b);
		}
		public static Int2 operator *(Int2 a, int b)
		{
			return new Int2(a.x * b, a.y * b);
		}

		public static Int2 operator -(int a, Int2 b)
		{
			Int2 r;
			r.x = a - b.x;
			r.y = a - b.y;
			return r;
		}

		public static Int2 operator +(Int2 a, int b)
		{
			Int2 r;
			r.x = a.x + b;
			r.y = a.y + b;
			return r;
		}

		public static Int2 operator +(int a, Int2 b)
		{
			Int2 r;
			r.x = a + b.x;
			r.y = a + b.y;
			return r;
		}

		public static Int2 operator -(Int2 a)
		{
			return new Int2(-a.x, -a.y);
		}

		public float Length
		{
			get { return (float)Math.Sqrt(x * x + y * y); }
		}

		public int SqLength
		{
			get { return x*x + y*y; }
		}

		public static bool operator ==(Int2 a, Int2 b)
		{
			return a.x == b.x && a.y == b.y;
		}
		public static bool operator !=(Int2 a, Int2 b)
		{
			return a.x != b.x || a.y != b.y;
		}

		public bool Equals(Int2 other)
		{
			return other == this;
		}
		bool IEquatable<Int2>.Equals(Int2 other)
		{
			return other == this;
		}

		public override string ToString()
		{
			return $"({x},{y})";
		}

		[JsonIgnore]
		[XmlIgnore]
		public Int2 YX
		{
			get
			{
				return new Int2(y, x);
			}
			set
			{
				x = value.y;
				y = value.x;
			}
		}

		/// <summary>
		/// Returning X*Y, interpreting the Int2 as a 2D size
		/// </summary>
		public int Area { get { return x * y; } }
	}
	public struct Int3 : IEquatable<Int3>
	{
		public Int3(Int2 p)
		{
			this.x = p.x; this.y = p.y; this.z = 0;
		}
		public Int3(int x, int y, int z=0)
		{
			this.x = x; this.y = y;
			this.z = z;
		}
		[XmlAttribute]
		public int x, y,z;

		public void Clamp(int w, int h, int d)
		{
			if (x < 0) x = 0; if (x > w - 1) x = w - 1;
			if (y < 0) y = 0; if (y > h - 1) y = h - 1;
			if (z < 0) z = 0; if (z > d - 1) z = d - 1;
		}

		public static bool operator ==(Int3 a, Int3 b)
		{
			return a.x == b.x && a.y == b.y && a.z == b.z;
		}
		public static bool operator !=(Int3 a, Int3 b)
		{
			return a.x != b.x || a.y != b.y || a.z != b.z;
		}

		public static Int3 operator +(Int3 a, Int3 b)
		{
			Int3 r;
			r.x = a.x + b.x;
			r.y = a.y + b.y;
			r.z = a.z + b.z;
			return r;
		}

		public static Int3 operator -(Int3 a, Int3 b)
		{
			Int3 r;
			r.x = a.x - b.x;
			r.y = a.y - b.y;
			r.z = a.z - b.z;
			return r;
		}

		public static Int3 operator -(Int3 a, int b)
		{
			Int3 r;
			r.x = a.x - b;
			r.y = a.y - b;
			r.z = a.z - b;
			return r;
		}

		public static Int3 operator -(int a, Int3 b)
		{
			Int3 r;
			r.x = a - b.x;
			r.y = a - b.y;
			r.z = a - b.z;
			return r;
		}

		public static Int3 operator +(Int3 a, int b)
		{
			Int3 r;
			r.x = a.x + b;
			r.y = a.y + b;
			r.z = a.z + b;
			return r;
		}

		public static Int3 operator +(int a, Int3 b)
		{
			Int3 r;
			r.x = a + b.x;
			r.y = a + b.y;
			r.z = a + b.z;
			return r;
		}

		public static Int3 operator -(Int3 a)
		{
			return new Int3(-a.x, -a.y, -a.z);
		}

		public float Length
		{
			get { return (float)Math.Sqrt(x * x + y * y + z * z); }
		}

		public override string ToString()
		{
			return string.Format("Int3({0},{1},{2})", x, y, z);
		}

		[JsonIgnore]
		[XmlIgnore]
		public Int2 XY {
			get { return new Int2(x, y); }
			set { x = value.x; y = value.y; } 
		}

		[JsonIgnore]
		[XmlIgnore]
		public Int2 XZ
		{
			get { return new Int2(x, z); }
			set { x = value.x; z = value.y; }
		}
		[JsonIgnore]
		[XmlIgnore]
		public Int2 YZ
		{
			get { return new Int2(y, z); }
			set { y = value.x; z = value.y; }
		}

		public bool Equals(Int3 other)
		{
			return other == this;
		}

		bool IEquatable<Int3>.Equals(Int3 other)
		{
			return other == this;
		}

		public override int GetHashCode()
		{
			return x + y*17 + z*17*17;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is Int3 && Equals((Int3)obj);
		}
	}

}
