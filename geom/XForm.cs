using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace geom
{
	[System.ComponentModel.TypeConverter(typeof(StructTypeConverter<XForm>))]
	public struct XForm
	{
		public Vector2 pos;
		public Vector2 xAxis, yAxis;

		public static XForm Identity
		{
			get
			{
				return new XForm()
				{
					xAxis = new Vector2(1, 0),
					yAxis = new Vector2(0, 1)
				};
			}
		}

		public XForm(Vector2 center, Vector2 scale, float angleInRadians)
		{
			pos = center;
			Vector2 xdir = Vector2.FromAngle(angleInRadians);
			xAxis = xdir * scale.x;
			yAxis = xdir.Ortho * scale.y;
		}

		public XForm(Vector2 center, float radians)
		{
			Vector2 xdir = Vector2.FromAngle(radians);
			xAxis = xdir;
			yAxis = xdir.Ortho;
			pos = center;
		}

		public XForm(Vector2 center, Vector2 dir)
		{
			pos = center;
			xAxis = dir;
			yAxis = dir.Ortho;
		}

		public Vector2 Transform(Vector2 u)
		{
			return u.x * xAxis + u.y * yAxis + pos;
		}

		public XForm Transform(XForm u)
		{
			return new XForm()
			{
				pos = Transform(u.pos),
				xAxis = u.xAxis.x * xAxis + u.xAxis.y * yAxis,
				yAxis = u.yAxis.x * xAxis + u.yAxis.y * yAxis
			};
		}

		public XForm Moved(Vector2 s)
		{
			return new XForm()
			{
				xAxis = xAxis,
				yAxis = yAxis,
				pos = pos + s
			};
		}

	}
}
