// EagleXML 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using geom;

namespace EagleXML
{
	public class LibraryInfo
	{
		eagle eagle;
		Dictionary<string, LibraryRef> libraryMap = new Dictionary<string, LibraryRef>();
		public class LibraryRef
		{
			public library lib;
			public Dictionary<string, package> packages = new Dictionary<string, package>();
		}

		public LibraryInfo(eagle e) {
			eagle=e;
			if (e.Schematic != null)
				RegisterLibraries(e.Schematic.libraries.library);
			if (e.Board != null)
				RegisterLibraries(e.Board.Libraries);
		}

		void RegisterLibraries(library[] libs)
		{
			foreach (var l in libs)
			{
				var lr = new LibraryRef() { lib = l, packages = new Dictionary<string, package>() };
				libraryMap[l.name] = lr;
				foreach (var pk in l.packages.package)
					lr.packages[pk.name] = pk;
			}
		}

		public package FindPackage(string library, string package)
		{
			return libraryMap[library].packages[package];
		}
	}

	partial class eagle
	{
		public board Board
		{
			get { return drawing.Item as board; }
		}
		public schematic Schematic
		{
			get { return drawing.Item as schematic; }
		}

		public layer[] Layers
		{
			get { return drawing.layers.layer; }
		}
	}

	public partial class drawing
	{
		public layer GetLayer(string p)
		{
			return layers.layer.First(l => l.name == p);
		}

		public layer[] GetLayers(params string[] names)
		{
			return names.Select(n => GetLayer(n)).ToArray();
		}

		public Box2 ComputeSize()
		{
			var l = GetLayer("Dimension");

			if (!(this.Item is board))
				return new Box2();

			HashSet<int> layers = new HashSet<int>();
			layers.Add(l.number);
			return Util.GetDrawablesDimensions((Item as board).plain, layers, XForm.Identity);
		}
	}

	public partial class layer
	{
		public override string ToString()
		{
			return name;
		}
	}

	public class PackageRef
	{
		public EagleXML.library library;
		public EagleXML.package package;

		public EagleXML.IComponentPad FindPad(string a_padName)
		{
			return package.FindPad(a_padName);
		}

		public override string ToString()
		{
			return library.name + "_" + package.name;
		}
	}

	public partial class board
	{
		public IEnumerable<element> Elements { get { return elements.element; } }
		public library[] Libraries { get { return libraries.library; } }

		Dictionary<string, PackageRef> libPackageMap;

		public void ListComponents()
		{
			var lm = LibraryPackageMap;
			int i = 0;
			foreach (var item in lm.Keys)
				Debug.WriteLine("{0}: {1}", i++, item);
		}

		[XmlIgnore]
		public Dictionary<string, PackageRef> LibraryPackageMap
		{
			get
			{
				if (libPackageMap == null)
				{
					var lm = new Dictionary<string, PackageRef>();
					foreach (library l in Libraries)
						foreach (package p in l.packages.package)
							lm[l.name + "_" + p.name] = new EagleXML.PackageRef()
							{
								library = l,
								package = p
							};
					libPackageMap = lm;
				}
				return libPackageMap;
			}
		}
	}

	public partial class library
	{
		public override string ToString()
		{
			return name;
		}
	}


	public interface IComponentPad
	{
		string Name { get; }
		Vector2 Position { get; }
		geom.Polygon GetPadPolygon();
	}

	public interface IDrawable
	{
		geom.Polygon GetPolygonOrLine();
		void Draw(System.Drawing.Graphics g, Pen pen);
		int Layer { get; }
		float LineWidth { get; }
	}

	public partial class polygon : IDrawable
	{
		[XmlIgnore]
		geom.Polygon pl;

		public geom.Polygon GetPolygonOrLine()
		{
			return new geom.Polygon(Array.ConvertAll(vertex,
				vrt => new Vector2(vrt.x, vrt.y)));
		}

		[XmlIgnore]
		public float LineWidth
		{
			get { return width; }
		}

		public void Draw(System.Drawing.Graphics g, Pen pen)
		{
			if (pl == null)
				pl = GetPolygonOrLine();

			g.DrawPolygon(pen, Array.ConvertAll(pl.verts, vrt => new PointF(vrt.x, vrt.y)));
		}

		public int Layer { get { return layer; } }
	}

	public partial class package
	{
		public IComponentPad FindPad(string padName)
		{
			foreach(var c in Pads) {
				if (c.Name == padName)
					return c;
			}
			throw new ArgumentException("Could not found pad named in " + padName + " in package " + name);
		}

		public IEnumerable<IComponentPad> Pads
		{
			get
			{
				return Items.Where(item => item is IComponentPad).Cast<IComponentPad>();
			}
		}
		public override string ToString()
		{
			return name;
		}

		public Polygon[] GetPackagePolygons(XForm xform, HashSet<int> layers = null)
		{
			List<Polygon> polygons = new List<Polygon>();
			foreach (var elem in Items)
			{
				var drawable = elem as EagleXML.IDrawable;
				if (drawable == null)
					continue;

				if (layers == null || layers.Contains(drawable.Layer))
				{
					var pl = drawable.GetPolygonOrLine();
					if (pl != null)
						polygons.Add(new Polygon(Array.ConvertAll(pl.verts, v => xform.Transform(v))));
				}
			}
			return polygons.ToArray();
		}

		public Polygon[] GetPackageOutline(XForm xform, HashSet<int> layers = null)
		{
			Polygon[] polygons = GetPackagePolygons(xform, layers);

			PolyShape shape = new PolyShape();
			shape.AddOverlappingPolygons(polygons);

			PolyMerge merger = new PolyMerge(shape.ToPolygons());
			merger.MergePolygons();
			return merger.GetPolygons();
		}
	}

	public partial class @class
	{
		public override string ToString()
		{
			return name;
		}
	}

	public partial class wire : IDrawable
	{
		public Polygon GetPolygonOrLine()
		{
			return new Polygon(new Vector2[] { new Vector2(x1, y1), new Vector2(x2, y2) });
		}

		public void Draw(Graphics g, Pen pen)
		{
			g.DrawLine(pen, x1, y1, x2, y2);
		}


		public int Layer
		{
			get { return layer; }
		}


		public float LineWidth
		{
			get { return width; }
		}
	}

	public partial class element
	{
		public element(PackageRef pr, Vector2 position)
		{
			library = pr.library.name;
			package = pr.package.name;
			x = position.x;
			y = position.y;
		}

		public override string ToString()
		{
			return "elem: "+ name;
		}
		
		public XForm GetXForm()
		{
			return Util.GetXForm(rot, x, y);
		}

		public string Library_Package
		{
			get { return library + "_" + package; }
		}
	}

	public partial class rectangle : IDrawable
	{
		public geom.Polygon GetPolygonOrLine()
		{
			return new Polygon(new Vector2[]{
				new Vector2(x1,y1),new Vector2(x1,y2),new Vector2(x2,y2),new Vector2(x2,y1)
			}).Transform(Util.GetXForm(rot));
		}

		public void Draw(Graphics g, Pen pen)
		{
			Util.DrawPolygon(GetPolygonOrLine(), pen, g);
		}

		public int Layer { get { return layer; } }


		public float LineWidth
		{
			get { return 0; }
		}
	}

	public partial class pad : IComponentPad
	{
		public override string ToString()
		{
			return "pad:" + this.name;
		}

		public string Name
		{
			get { return this.name; }
		}

		public Vector2 Position
		{
			get { return new Vector2(x, y); }
		}


		Polygon IComponentPad.GetPadPolygon()
		{
			return null;
		}
	}

	public partial class smd : IDrawable, IComponentPad
	{
		geom.Polygon drawPoly;

		public override string ToString()
		{
			return "smd: "+name;
		}

		[XmlIgnore]
		public int Layer
		{
			get { return layer; }
		}

		public geom.Polygon GetPolygonOrLine()
		{
			return new Polygon(new Vector2[]{
					new Vector2(-dx/2,-dy/2),new Vector2(-dx/2,dy/2),new Vector2(dx/2,dy/2),new Vector2(dx/2,-dy/2)
				}).Transform(GetXForm());
		}

		public XForm GetXForm()
		{
			return Util.GetXForm(rot, x, y);
		}

		public void Draw(System.Drawing.Graphics g, Pen pen)
		{
			if (drawPoly == null)
				drawPoly = GetPolygonOrLine();

			Util.DrawPolygon(drawPoly, pen, g);
		}

		public string Name
		{
			get { return name; }
		}

		public Vector2 Position
		{
			get { return new Vector2(x,y); }
		}

		string IComponentPad.Name
		{
			get { return name; }
		}

		Vector2 IComponentPad.Position
		{
			get { return Position; }
		}


		public float LineWidth
		{
			get { return 0; }
		}



		Polygon IComponentPad.GetPadPolygon()
		{
			return GetPolygonOrLine();
		}
	}

	public class Util
	{
		public static Box2 GetDrawablesDimensions(object[] objs, ISet<int> layers, XForm xform)
		{
			var drawables = objs.Where(i => i is EagleXML.IDrawable).Cast<EagleXML.IDrawable>();
			return GetDrawablesDimensions(drawables, layers, xform);
		}

		public static Box2 GetDrawablesDimensions(IEnumerable<IDrawable> elems, ISet<int> layers, XForm xform)
		{
			var polylist = GetDrawablesPolygons(elems, layers);

			Box2 box = new Box2();
			bool first=true;
			foreach (var pl in polylist)
			{
				Polygon tr = pl.Transform(xform);
				if (first)
					box = tr.BBox;
				else
					box.Extend(tr);
			}
			return box;
		}

		public static IEnumerable<Polygon> GetDrawablesPolygons(IEnumerable<IDrawable> elems, ISet<int> layers)
		{
			List<Polygon> pl = new List<Polygon>();
			foreach (var e in elems)
				if (layers.Contains(e.Layer)) pl.Add(e.GetPolygonOrLine());
			return pl;
		}


		static T ReadXMLString<T>(string xml) where T : class
		{
			XmlSerializer s = new XmlSerializer(typeof(T));
			using (StringReader r = new StringReader(xml))
			{
				return (T)s.Deserialize(r);
			}
		}

		static T ReadXMLFile<T>(string filename) where T : class
		{
			return ReadXMLString<T>(File.ReadAllText(filename));
		}

		public static string ObjectToXML<T>(T pk)
		{
			XmlSerializer s = new XmlSerializer(typeof(T));
			using (StringWriter sw = new StringWriter())
			{
				s.Serialize(sw, pk);
				return sw.ToString();
			}
		}

		public static void WriteXMLFile<T>(string filename, T obj)
		{
			File.WriteAllText(filename, ObjectToXML(obj));
		}

		public static string RotationString(float rot, bool mirror)
		{
			if (!mirror && rot == 0) return null;
			return string.Format("{0}R{1}", mirror ? "M" : "", (int)rot);
		}

		public static XForm GetXForm(string rot, float x = 0, float y = 0)
		{
			if (rot == null)
				return XForm.Identity;

			float mirror = 1;
			if (rot[0] == 'M')
			{
				mirror = -1;
				rot = rot.Substring(1);
			}
			if (rot[0] == 'S')
				rot = rot.Substring(1);

			float angleInDegrees = float.Parse(rot.Substring(1)) * (float)Math.PI / 180.0f;
			var xform = new XForm(new Vector2(x, y), new Vector2(mirror, 1.0f), angleInDegrees);

			return xform;
		}

		public static void DrawPolygon(Polygon pl, Pen pen, Graphics g)
		{
			g.DrawPolygon(pen, 
				Array.ConvertAll(pl.verts, v => new PointF(v.x, v.y)));
		}

		public static eagle ReadXML(string eaglexml)
		{
			var e = ReadXMLString<eagle>(eaglexml);
			return e; 
		}

		public static eagle ReadFile(string filename)
		{
			return ReadXML(File.ReadAllText(filename));
		}

		public static void WriteFile(eagle data, string filename)
		{
			WriteXMLFile(filename, data);
		}
	}
}
